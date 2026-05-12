using System.Globalization;
using FamilyNido.Api.Features.MemberAgenda;
using FamilyNido.Api.Options;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Domain.Meals;
using FamilyNido.Domain.Notifications;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Hosted service that wakes every few minutes, walks through every family in
/// the database, and queues a "today in FamilyNido" digest for each member that
/// hasn't received one yet on their family's local date.
/// </summary>
/// <remarks>
/// <para>
/// Idempotency is delegated to the <see cref="EmailDigestRun"/> table: the
/// composite primary key <c>(UserId, LocalDate)</c> doubles as a lock, so the
/// service inserts the row first and only queues the email when the insert
/// succeeded — a duplicate from a concurrent tick fails the insert and the
/// branch is skipped.
/// </para>
/// <para>
/// The morning hour is read from <see cref="EmailOptions.DigestHour"/> and is
/// applied per family in the family's IANA timezone. Members with the master
/// switch off, the digest channel off, or no linked email are silently
/// skipped — but the run row is still inserted so we don't keep checking them
/// every five minutes for the rest of the day.
/// </para>
/// </remarks>
public sealed class EmailDigestBackgroundService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(45);
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailDigestBackgroundService> _logger;

    /// <summary>Primary constructor.</summary>
    public EmailDigestBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<EmailOptions> emailOptions,
        TimeProvider timeProvider,
        ILogger<EmailDigestBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _emailOptions = emailOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await TickOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task TickOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var dispatcher = sp.GetRequiredService<EmailDispatchService>();

            var emailOpts = _emailOptions.CurrentValue;
            var nowUtc = _timeProvider.GetUtcNow();

            var families = await db.Families.AsNoTracking().ToListAsync(stoppingToken);

            foreach (var family in families)
            {
                if (stoppingToken.IsCancellationRequested) return;
                await ProcessFamilyAsync(db, dispatcher, family.Id, family.TimeZone, nowUtc, emailOpts, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — the loop returns from Task.Delay above.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email digest tick failed at the top level.");
        }
    }

    private async Task ProcessFamilyAsync(
        ApplicationDbContext db,
        EmailDispatchService dispatcher,
        Guid familyId,
        string ianaTimeZone,
        DateTimeOffset nowUtc,
        EmailOptions emailOpts,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Family {FamilyId} has unknown timezone {Tz}; falling back to UTC.", familyId, ianaTimeZone);
            tz = TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTime(nowUtc, tz);
        if (localNow.Hour < emailOpts.DigestHour)
        {
            return;
        }

        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        // Recipients are members of this family that have a linked user — children
        // without an account never receive emails.
        var recipients = await db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.IsActive && m.UserId != null && m.User!.Email != string.Empty)
            .Select(m => new RecipientRow(m.Id, m.DisplayName, m.UserId!.Value, m.User!.Email, m.User!.PreferredLanguage))
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var alreadyProcessed = await db.EmailDigestRuns
                .AnyAsync(r => r.UserId == recipient.UserId && r.LocalDate == localDate, cancellationToken);
            if (alreadyProcessed) continue;

            // Mark the user as processed first so a concurrent tick — or a crash
            // partway through — never double-sends. We swallow unique-constraint
            // races silently (another instance already grabbed it).
            db.EmailDigestRuns.Add(new EmailDigestRun
            {
                UserId = recipient.UserId,
                LocalDate = localDate,
                SentAt = nowUtc,
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Race with another tick — skip silently and rely on the row that won.
                db.ChangeTracker.Clear();
                continue;
            }

            var prefs = await db.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == recipient.UserId, cancellationToken);

            var emailEnabled = prefs?.EmailEnabled ?? true;
            var digestEnabled = prefs?.DigestEnabled ?? true;
            if (!emailEnabled || !digestEnabled) continue;

            var content = await BuildContentAsync(db, familyId, recipient, localDate, tz, cancellationToken);
            if (content.IsEmpty) continue;

            var (subject, html) = EmailTemplates.Digest(recipient.DisplayName, content, emailOpts.AppBaseUrl, recipient.Language);
            dispatcher.Queue(new EmailMessage(recipient.Email, subject, html));
        }
    }

    /// <summary>
    /// Build the per-recipient digest content for a given local date. Exposed
    /// internally so <see cref="SendMyDigest"/> (manual trigger) reuses the
    /// exact same composition logic as the scheduled tick.
    /// </summary>
    internal static async Task<EmailTemplates.DigestContent> BuildContentAsync(
        ApplicationDbContext db,
        Guid familyId,
        RecipientRow recipient,
        DateOnly localDate,
        TimeZoneInfo tz,
        CancellationToken cancellationToken)
    {
        var tomorrow = localDate.AddDays(1);
        var startUtc = new DateTimeOffset(localDate.ToDateTime(TimeOnly.MinValue), tz.GetUtcOffset(localDate.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
        var endUtc = new DateTimeOffset(tomorrow.ToDateTime(TimeOnly.MinValue), tz.GetUtcOffset(tomorrow.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();

        // ── Tasks: every chore scheduled for today in the family that is still
        // pending. We surface the whole family roster (not just "yours")
        // because the digest is also useful as a "who is doing what today"
        // overview, and family-internal unassigned chores ("Sacar la basura")
        // would otherwise be invisible when nobody is set as responsible. The
        // role label tells the recipient at a glance whether they need to act.
        // Tasks already completed for today are filtered out — the dashboard
        // widget still shows them (with a strike-through), but in the email
        // they would just be noise.
        var rawTasks = await db.HouseholdTasks
            .AsNoTracking()
            .Include(t => t.ResponsibleMember)
            .Include(t => t.RelatedMembers)
            .Include(t => t.Completions)
            .Where(t => t.FamilyId == familyId && !t.IsArchived)
            .ToListAsync(cancellationToken);

        var taskItems = new List<EmailTemplates.DigestItem>();
        foreach (var task in rawTasks)
        {
            if (!task.HasOccurrenceOn(localDate)) continue;
            if (task.Completions.Any(c => c.OccurrenceDate == localDate)) continue;

            string role;
            if (task.ResponsibleMemberId == recipient.MemberId)
            {
                role = "Responsable: tú";
            }
            else if (task.RelatedMembers.Any(m => m.Id == recipient.MemberId))
            {
                role = task.ResponsibleMember is { } rm
                    ? $"Relacionado · responsable {rm.DisplayName}"
                    : "Relacionado";
            }
            else if (task.ResponsibleMember is { } rm2)
            {
                role = $"Responsable: {rm2.DisplayName}";
            }
            else
            {
                role = "Sin responsable";
            }

            var detail = task.TimeOfDay is { } t
                ? $"{role} · {t:HH:mm}"
                : role;
            taskItems.Add(new EmailTemplates.DigestItem(task.Title, detail));
        }

        // ── Events: intersect today's local window. Surface events that
        // belong to the recipient (by linked-calendar or per-event tagging)
        // AND events on calendars without an assigned member — those are the
        // "shared / family" calendars and previously stayed silent in the
        // digest, even though every adult cared about them.
        var events = await db.CalendarEvents
            .AsNoTracking()
            .Include(e => e.LinkedCalendar)
            .Include(e => e.RelatedMembers)
            .Where(e => e.FamilyId == familyId
                && e.StartAt < endUtc
                && e.EndAt > startUtc
                && (e.LinkedCalendar!.FamilyMemberId == recipient.MemberId
                    || e.LinkedCalendar.FamilyMemberId == null
                    || e.RelatedMembers.Any(m => m.Id == recipient.MemberId)))
            .OrderBy(e => e.StartAt)
            .ToListAsync(cancellationToken);

        var eventItems = events.Select(e =>
        {
            string detail;
            if (e.IsAllDay)
            {
                detail = string.IsNullOrEmpty(e.Location) ? "Todo el día" : $"Todo el día · {e.Location}";
            }
            else
            {
                var startLocal = TimeZoneInfo.ConvertTime(e.StartAt, tz);
                var endLocal = TimeZoneInfo.ConvertTime(e.EndAt, tz);
                var range = $"{startLocal:HH:mm}–{endLocal:HH:mm}";
                detail = string.IsNullOrEmpty(e.Location) ? range : $"{range} · {e.Location}";
            }
            return new EmailTemplates.DigestItem(e.Title, detail);
        }).ToList();

        // ── Agenda: members away from home today (work, regular activities,
        // ad-hoc travel). Reuses GetMemberAgendaOverview.Resolve so the digest
        // and the dashboard widget agree on what "today" looks like.
        var agendaPatterns = await db.MemberAgendaPatterns
            .AsNoTracking()
            .Include(p => p.FamilyMember)
            .Where(p => p.FamilyId == familyId)
            .ToListAsync(cancellationToken);

        var agendaExceptions = await db.MemberAgendaExceptions
            .AsNoTracking()
            .Include(e => e.FamilyMember)
            .Where(e => e.FamilyId == familyId && e.Date == localDate)
            .ToListAsync(cancellationToken);

        var resolvedAgenda = GetMemberAgendaOverview.Handler.Resolve(
            agendaPatterns, agendaExceptions, localDate, localDate);

        var memberNamesById = agendaPatterns
            .Where(p => p.FamilyMember is not null)
            .GroupBy(p => p.FamilyMemberId)
            .ToDictionary(g => g.Key, g => g.First().FamilyMember!.DisplayName);
        foreach (var ex in agendaExceptions)
        {
            if (ex.FamilyMember is not null)
            {
                memberNamesById[ex.FamilyMemberId] = ex.FamilyMember.DisplayName;
            }
        }

        var agendaItems = resolvedAgenda
            .Where(r => r.IsAway)
            .Select(r =>
            {
                var name = memberNamesById.GetValueOrDefault(r.MemberId, "?");
                var detail = new List<string>();
                if (!string.IsNullOrEmpty(r.Location)) detail.Add(r.Location!);
                if (r.StartTime is { } start && r.EndTime is { } end)
                {
                    detail.Add($"{start:HH\\:mm}–{end:HH\\:mm}");
                }
                else if (r.StartTime is { } onlyStart)
                {
                    detail.Add($"desde las {onlyStart:HH\\:mm}");
                }
                return new EmailTemplates.DigestItem(
                    $"{name} · {r.Label}",
                    detail.Count == 0 ? null : string.Join(" · ", detail));
            })
            .ToList();

        // ── School: holiday banner, bus pickup, extracurriculars where this
        // member is the kid OR a caretaker (so the responsible adult always
        // gets the "you're picking up Bob today" line in their digest).
        var schoolItems = new List<EmailTemplates.DigestItem>();

        var holidayToday = await db.SchoolHolidays
            .AsNoTracking()
            .Where(h => h.FamilyId == familyId && h.StartDate <= localDate && h.EndDate >= localDate)
            .Select(h => h.Label)
            .FirstOrDefaultAsync(cancellationToken);

        if (holidayToday is not null)
        {
            schoolItems.Add(new EmailTemplates.DigestItem(
                $"Festivo: {holidayToday}", "Hoy no hay cole."));
        }
        else
        {
            // School day rows where this recipient is the kid OR a caretaker.
            // We materialise both sides because for adults the digest doubles
            // as their "to-do for the day".
            var daySchedule = await db.SchoolDaySchedules
                .AsNoTracking()
                .Include(s => s.FamilyMember)
                .Include(s => s.DropoffMember)
                .Include(s => s.PickupMember)
                .Where(s => s.FamilyMember!.FamilyId == familyId
                    && s.DayOfWeek == localDate.DayOfWeek
                    && (s.FamilyMemberId == recipient.MemberId
                        || s.DropoffMemberId == recipient.MemberId
                        || s.PickupMemberId == recipient.MemberId))
                .ToListAsync(cancellationToken);

            var dayExceptions = await db.SchoolDayExceptions
                .AsNoTracking()
                .Include(e => e.FamilyMember)
                .Include(e => e.DropoffMember)
                .Include(e => e.PickupMember)
                .Where(e => e.FamilyId == familyId && e.Date == localDate
                    && (e.FamilyMemberId == recipient.MemberId
                        || e.DropoffMemberId == recipient.MemberId
                        || e.PickupMemberId == recipient.MemberId))
                .ToListAsync(cancellationToken);

            // Index exceptions by (kidId) so we can override the schedule.
            var exceptionByKid = dayExceptions.ToDictionary(e => e.FamilyMemberId);

            // Profile defaults for the kids that show up today. Used to fall
            // back when the exception only overrides one slot of the times.
            var kidIds = daySchedule.Select(s => s.FamilyMemberId)
                .Concat(dayExceptions.Select(e => e.FamilyMemberId))
                .Distinct()
                .ToList();
            var profileTimes = kidIds.Count == 0
                ? []
                : await db.SchoolProfiles
                    .AsNoTracking()
                    .Where(p => kidIds.Contains(p.FamilyMemberId))
                    .Select(p => new { p.FamilyMemberId, p.MorningTime, p.AfternoonTime })
                    .ToListAsync(cancellationToken);
            var defaultsByKid = profileTimes.ToDictionary(
                p => p.FamilyMemberId,
                p => (Morning: p.MorningTime, Afternoon: p.AfternoonTime));
            (TimeOnly? Morning, TimeOnly? Afternoon) DefaultTimes(Guid kidId)
                => defaultsByKid.TryGetValue(kidId, out var t) ? t : (null, null);

            var daySeen = new HashSet<Guid>();
            foreach (var s in daySchedule)
            {
                daySeen.Add(s.FamilyMemberId);
                var defaults = DefaultTimes(s.FamilyMemberId);
                if (exceptionByKid.TryGetValue(s.FamilyMemberId, out var ex))
                {
                    AppendSchoolDayItem(schoolItems,
                        s.FamilyMember!.DisplayName,
                        ex.IsCancelled,
                        ex.DropoffMember?.DisplayName ?? s.DropoffMember?.DisplayName,
                        ex.PickupMember?.DisplayName ?? s.PickupMember?.DisplayName,
                        ex.MorningTime ?? defaults.Morning,
                        ex.AfternoonTime ?? defaults.Afternoon);
                }
                else
                {
                    AppendSchoolDayItem(schoolItems,
                        s.FamilyMember!.DisplayName,
                        isCancelled: false,
                        s.DropoffMember?.DisplayName,
                        s.PickupMember?.DisplayName,
                        defaults.Morning,
                        defaults.Afternoon);
                }
            }
            // Exceptions for kids that don't have a schedule row today.
            foreach (var ex in dayExceptions.Where(e => !daySeen.Contains(e.FamilyMemberId)))
            {
                var defaults = DefaultTimes(ex.FamilyMemberId);
                AppendSchoolDayItem(schoolItems,
                    ex.FamilyMember!.DisplayName,
                    ex.IsCancelled,
                    ex.DropoffMember?.DisplayName,
                    ex.PickupMember?.DisplayName,
                    ex.MorningTime ?? defaults.Morning,
                    ex.AfternoonTime ?? defaults.Afternoon);
            }

            // Extracurriculars today where the recipient is the kid OR a default/override caretaker.
            var todaysExtras = await db.Extracurriculars
                .AsNoTracking()
                .Include(e => e.FamilyMember)
                .Include(e => e.DefaultDropoffMember)
                .Include(e => e.DefaultPickupMember)
                .Include(e => e.Exceptions.Where(x => x.Date == localDate))
                .Where(e => e.FamilyId == familyId
                    && !e.IsArchived
                    && e.StartDate <= localDate
                    && (e.EndDate == null || e.EndDate >= localDate))
                .ToListAsync(cancellationToken);

            foreach (var activity in todaysExtras)
            {
                if ((activity.WeeklyDays & ToMask(localDate.DayOfWeek)) == DayOfWeekMask.None) continue;

                var ex = activity.Exceptions.FirstOrDefault();
                var dropoffId = ex?.DropoffMemberId ?? activity.DefaultDropoffMemberId;
                var pickupId = ex?.PickupMemberId ?? activity.DefaultPickupMemberId;
                var isCancelled = ex?.IsCancelled ?? false;

                var involves = activity.FamilyMemberId == recipient.MemberId
                    || dropoffId == recipient.MemberId
                    || pickupId == recipient.MemberId;
                if (!involves) continue;

                if (isCancelled)
                {
                    schoolItems.Add(new EmailTemplates.DigestItem(
                        activity.Name,
                        $"Cancelado · {activity.FamilyMember!.DisplayName}"));
                    continue;
                }

                var time = $"{activity.StartTime:HH:mm}–{activity.EndTime:HH:mm}";
                var dropoffName = await ResolveDisplayNameAsync(db, dropoffId, cancellationToken);
                var pickupName = await ResolveDisplayNameAsync(db, pickupId, cancellationToken);
                var detailParts = new List<string>
                {
                    activity.FamilyMember!.DisplayName,
                    time,
                };
                if (!string.IsNullOrEmpty(activity.Location)) detailParts.Add(activity.Location!);
                if (dropoffName is not null) detailParts.Add($"lleva {dropoffName}");
                if (pickupName is not null) detailParts.Add($"recoge {pickupName}");
                schoolItems.Add(new EmailTemplates.DigestItem(activity.Name, string.Join(" · ", detailParts)));
            }
        }

        // ── Meals: today's planned lunch and dinner from the meal planner.
        var mealSlots = await db.MealPlanSlots
            .AsNoTracking()
            .Where(s => s.FamilyId == familyId && s.Date == localDate)
            .OrderBy(s => s.Slot)
            .ToListAsync(cancellationToken);

        var mealItems = mealSlots
            .Select(slot =>
            {
                var courses = new[] { slot.FirstCourse, slot.SecondCourse }
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToArray();
                var title = slot.Slot == MealSlot.Lunch ? "Comida" : "Cena";
                var detail = courses.Length == 0 ? null : string.Join(" · ", courses);
                return new EmailTemplates.DigestItem(title, detail);
            })
            .Where(i => !string.IsNullOrEmpty(i.Detail))
            .ToList();

        // ── Birthdays today and tomorrow (any active member of the family).
        var membersWithBirthday = await db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.IsActive && m.BirthDate != null)
            .Select(m => new { m.DisplayName, m.BirthDate })
            .ToListAsync(cancellationToken);

        var birthdayItems = new List<EmailTemplates.DigestItem>();
        foreach (var m in membersWithBirthday)
        {
            var bd = m.BirthDate!.Value;
            if (bd.Day == localDate.Day && bd.Month == localDate.Month)
            {
                var age = localDate.Year - bd.Year;
                birthdayItems.Add(new EmailTemplates.DigestItem(
                    $"¡Hoy es el cumple de {m.DisplayName}!",
                    $"Cumple {age} años"));
            }
            else if (bd.Day == tomorrow.Day && bd.Month == tomorrow.Month)
            {
                var age = tomorrow.Year - bd.Year;
                birthdayItems.Add(new EmailTemplates.DigestItem(
                    $"Mañana cumple {m.DisplayName}",
                    $"Cumplirá {age} años"));
            }
        }

        // ── Wall messages new since the last digest run (or last 24h if none).
        var since = nowUtcMinusOneDay();
        var lastRun = await db.EmailDigestRuns
            .AsNoTracking()
            .Where(r => r.UserId == recipient.UserId && r.LocalDate < localDate)
            .OrderByDescending(r => r.LocalDate)
            .Select(r => (DateTimeOffset?)r.SentAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastRun is { } lr && lr > since) since = lr;

        var newMessages = await db.WallMessages
            .AsNoTracking()
            .Where(w => w.FamilyId == familyId
                && w.CreatedAt > since
                && w.AuthorMemberId != recipient.MemberId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(5)
            .Select(w => new
            {
                w.Text,
                AuthorName = w.AuthorMember!.DisplayName,
            })
            .ToListAsync(cancellationToken);

        var wallItems = newMessages
            .Select(w => new EmailTemplates.DigestItem(w.AuthorName, EmailTemplates.MakeSnippet(w.Text, 140)))
            .ToList();

        return new EmailTemplates.DigestContent(
            taskItems, eventItems, agendaItems, mealItems, schoolItems, birthdayItems, wallItems);
    }

    /// <summary>Build a "lleva X · recoge Y" line for the school section, branching on cancellation.</summary>
    private static void AppendSchoolDayItem(
        List<EmailTemplates.DigestItem> sink,
        string kidName,
        bool isCancelled,
        string? dropoffName,
        string? pickupName,
        TimeOnly? morningTime,
        TimeOnly? afternoonTime)
    {
        if (isCancelled)
        {
            sink.Add(new EmailTemplates.DigestItem(kidName, "Hoy no hay cole"));
            return;
        }
        var parts = new List<string>();
        if (dropoffName is not null)
        {
            parts.Add(morningTime is { } m
                ? $"Lleva {dropoffName} a las {m:HH\\:mm}"
                : $"Lleva {dropoffName}");
        }
        if (pickupName is not null)
        {
            parts.Add(afternoonTime is { } a
                ? $"Recoge {pickupName} a las {a:HH\\:mm}"
                : $"Recoge {pickupName}");
        }
        if (parts.Count == 0 && afternoonTime is { } onlyTime)
        {
            parts.Add($"Sale a las {onlyTime:HH\\:mm}");
        }
        sink.Add(new EmailTemplates.DigestItem(
            kidName,
            parts.Count == 0 ? "Sin asignar" : string.Join(" · ", parts)));
    }

    private static async Task<string?> ResolveDisplayNameAsync(
        ApplicationDbContext db,
        Guid? memberId,
        CancellationToken cancellationToken)
    {
        if (memberId is null) return null;
        return await db.FamilyMembers
            .Where(m => m.Id == memberId)
            .Select(m => m.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DayOfWeekMask ToMask(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => DayOfWeekMask.Monday,
        DayOfWeek.Tuesday => DayOfWeekMask.Tuesday,
        DayOfWeek.Wednesday => DayOfWeekMask.Wednesday,
        DayOfWeek.Thursday => DayOfWeekMask.Thursday,
        DayOfWeek.Friday => DayOfWeekMask.Friday,
        DayOfWeek.Saturday => DayOfWeekMask.Saturday,
        DayOfWeek.Sunday => DayOfWeekMask.Sunday,
        _ => DayOfWeekMask.None,
    };

    private static DateTimeOffset nowUtcMinusOneDay() => DateTimeOffset.UtcNow.AddDays(-1);

    /// <summary>Compact carrier the digest builder needs about its recipient.</summary>
    internal readonly record struct RecipientRow(Guid MemberId, string DisplayName, Guid UserId, string Email, string Language);
}
