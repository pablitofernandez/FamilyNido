using FamilyNido.Api.Shared.Markdown;
using FamilyNido.Domain.Agenda;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Domain.Identity;
using FamilyNido.Domain.Meals;
using FamilyNido.Domain.School;
using FamilyNido.Domain.Wall;
using FamilyNido.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Bootstrap;

/// <summary>
/// Hosted service that drops a curated, screenshot-friendly scenario into an
/// empty database when running under the <c>Development</c> environment with
/// <c>Seed:Demo:Enabled=true</c>. Exists so the README screenshots can be
/// captured against a coherent fake family without anybody having to expose
/// their real data.
/// </summary>
/// <remarks>
/// <para>Idempotent at the family level: if a family with the configured name
/// already exists, the seeder bails. Re-seeding from scratch is therefore a
/// matter of recreating the dev volume
/// (<c>docker compose -f deploy/docker-compose.dev.yml down -v &amp;&amp; up -d</c>)
/// and restarting the API.</para>
/// <para>The seeder anchors every date to "today" in the family's configured
/// time zone so the captured screenshots feel current regardless of when they
/// are taken — tasks pending today are actually pending today, completions
/// from the past week truly fall in the past week, etc.</para>
/// </remarks>
public sealed class DemoDataSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly DemoSeedOptions _options;
    private readonly ILogger<DemoDataSeeder> _logger;

    /// <summary>Primary constructor.</summary>
    public DemoDataSeeder(
        IServiceProvider services,
        IOptions<DemoSeedOptions> options,
        ILogger<DemoDataSeeder> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(_options.AdminPassword))
        {
            _logger.LogWarning(
                "Demo seeder enabled but Seed:Demo:AdminPassword is empty — skipping.");
            return;
        }

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var markdown = scope.ServiceProvider.GetRequiredService<MarkdownRenderer>();

        var alreadySeeded = await db.Families
            .AnyAsync(f => f.Name == _options.FamilyName, cancellationToken);
        if (alreadySeeded)
        {
            _logger.LogInformation(
                "Demo seeder: family {FamilyName} already exists — skipping seed.",
                _options.FamilyName);
            return;
        }

        // Anchor every relative date to "today" in the family's timezone so the
        // screenshots stay coherent regardless of where they are captured.
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var todayLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var today = DateOnly.FromDateTime(todayLocal.DateTime);

        _logger.LogInformation(
            "Demo seeder: bootstrapping family {FamilyName} (today={Today}, tz={TimeZone}).",
            _options.FamilyName, today, _options.TimeZone);

        var family = await SeedFamilyAsync(db, cancellationToken);
        var members = await SeedMembersAsync(db, hasher, family, today, cancellationToken);
        await SeedTasksAsync(db, family, members, today, cancellationToken);
        await SeedWallAsync(db, markdown, family, members, cancellationToken);
        await SeedMealsAsync(db, family, today, cancellationToken);
        await SeedSchoolAsync(db, family, members, today, cancellationToken);
        await SeedAgendaAsync(db, family, members, cancellationToken);

        _logger.LogInformation(
            "Demo seeder: done. Log in as {AdminEmail} to browse.",
            _options.AdminEmail);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task<Family> SeedFamilyAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var family = new Family
        {
            Name = _options.FamilyName,
            TimeZone = _options.TimeZone,
            Locale = _options.Locale,
            Latitude = _options.Latitude,
            Longitude = _options.Longitude,
            LocationLabel = _options.LocationLabel,
        };
        db.Families.Add(family);
        await db.SaveChangesAsync(cancellationToken);
        return family;
    }

    private async Task<DemoMembers> SeedMembersAsync(
        ApplicationDbContext db,
        IPasswordHasher<User> hasher,
        Family family,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var dan = await CreateAdultAsync(
            db, hasher, family,
            _options.AdminEmail, _options.AdminDisplayName, _options.AdminPassword,
            colorHex: "#3B82F6", role: FamilyRole.Admin, cancellationToken);

        FamilyMember eve;
        if (!string.IsNullOrEmpty(_options.PartnerPassword))
        {
            eve = await CreateAdultAsync(
                db, hasher, family,
                _options.PartnerEmail, _options.PartnerDisplayName, _options.PartnerPassword,
                colorHex: "#10B981", role: FamilyRole.Adult, cancellationToken);
        }
        else
        {
            eve = new FamilyMember
            {
                FamilyId = family.Id,
                DisplayName = _options.PartnerDisplayName,
                MemberType = MemberType.Adult,
                ColorHex = "#10B981",
                ContactEmail = _options.PartnerEmail,
            };
            db.FamilyMembers.Add(eve);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Kids: derive credible ages from "today" so the screenshots look
        // consistent independently of the calendar year.
        var alice = new FamilyMember
        {
            FamilyId = family.Id,
            DisplayName = "Alice",
            MemberType = MemberType.Child,
            ColorHex = "#EC4899",
            BirthDate = today.AddYears(-9).AddMonths(-2),
        };
        var bob = new FamilyMember
        {
            FamilyId = family.Id,
            DisplayName = "Bob",
            MemberType = MemberType.Child,
            ColorHex = "#F59E0B",
            BirthDate = today.AddYears(-6).AddMonths(-4),
        };
        db.FamilyMembers.AddRange(alice, bob);
        await db.SaveChangesAsync(cancellationToken);

        return new DemoMembers(dan, eve, alice, bob);
    }

    private async Task<FamilyMember> CreateAdultAsync(
        ApplicationDbContext db,
        IPasswordHasher<User> hasher,
        Family family,
        string email,
        string displayName,
        string password,
        string colorHex,
        FamilyRole role,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            Role = role,
            PreferredLanguage = _options.PreferredLanguage,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        db.UserCredentials.Add(new UserCredential
        {
            UserId = user.Id,
            Provider = IdentityProvider.Local,
            PasswordHash = hasher.HashPassword(user, password),
        });

        var member = new FamilyMember
        {
            FamilyId = family.Id,
            DisplayName = displayName,
            MemberType = MemberType.Adult,
            ColorHex = colorHex,
            ContactEmail = email,
            UserId = user.Id,
        };
        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);
        return member;
    }

    private static async Task SeedTasksAsync(
        ApplicationDbContext db,
        Family family,
        DemoMembers m,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // Mix of recurring + single-shot + floating so each tab on the tasks
        // page has visible content, plus a tail of past completions that feeds
        // the scoreboard widget.
        var trash = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Take out the trash",
            Category = "Home",
            Recurrence = RecurrenceMode.Daily,
            StartDate = today.AddDays(-14),
            Points = 2,
            CreatedByMemberId = m.Dan.Id,
        };
        var dishwasher = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Empty the dishwasher",
            Category = "Kitchen",
            Recurrence = RecurrenceMode.Daily,
            StartDate = today.AddDays(-14),
            Points = 3,
            CreatedByMemberId = m.Eve.Id,
        };
        var vacuum = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Vacuum the living room",
            Category = "Cleaning",
            Recurrence = RecurrenceMode.Weekly,
            WeeklyDays = DayOfWeekMask.Saturday,
            StartDate = today.AddDays(-30),
            Points = 5,
            CreatedByMemberId = m.Dan.Id,
        };
        var plants = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Water the plants",
            Category = "Home",
            Recurrence = RecurrenceMode.Weekly,
            WeeklyDays = DayOfWeekMask.Wednesday | DayOfWeekMask.Saturday,
            StartDate = today.AddDays(-30),
            Points = 2,
            CreatedByMemberId = m.Eve.Id,
        };
        var bathroom = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Clean the bathroom",
            Category = "Cleaning",
            Recurrence = RecurrenceMode.Weekly,
            WeeklyDays = DayOfWeekMask.Sunday,
            StartDate = today.AddDays(-30),
            Points = 5,
            CreatedByMemberId = m.Dan.Id,
        };
        var rent = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Pay the rent",
            Category = "Bills",
            Recurrence = RecurrenceMode.Monthly,
            MonthlyDay = 1,
            StartDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-3),
            Points = 5,
            CreatedByMemberId = m.Dan.Id,
        };
        var ballet = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Pick up Alice from ballet",
            Category = "Kids",
            Recurrence = RecurrenceMode.None,
            StartDate = today,
            DueDate = today,
            Points = 3,
            CreatedByMemberId = m.Eve.Id,
            ResponsibleMemberId = m.Eve.Id,
        };
        var bread = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Buy bread on the way home",
            Category = "Errands",
            Recurrence = RecurrenceMode.None,
            StartDate = today,
            IsFloating = true,
            Points = 1,
            CreatedByMemberId = m.Dan.Id,
        };
        var grandma = new HouseholdTask
        {
            FamilyId = family.Id,
            Title = "Call grandma",
            Category = "Family",
            Recurrence = RecurrenceMode.None,
            StartDate = today,
            DueDate = today.AddDays(1),
            Points = 2,
            CreatedByMemberId = m.Eve.Id,
        };

        db.HouseholdTasks.AddRange(trash, dishwasher, vacuum, plants, bathroom, rent, ballet, bread, grandma);
        await db.SaveChangesAsync(cancellationToken);

        // Past completions sprinkled across the last two weeks so the
        // scoreboard has data and the per-task history feels lived-in.
        var now = DateTimeOffset.UtcNow;
        var completions = new List<TaskCompletion>
        {
            // Dan is on the trash this week.
            new() { TaskId = trash.Id, OccurrenceDate = today.AddDays(-1), CompletedByMemberId = m.Dan.Id, CompletedAt = now.AddDays(-1) },
            new() { TaskId = trash.Id, OccurrenceDate = today.AddDays(-3), CompletedByMemberId = m.Dan.Id, CompletedAt = now.AddDays(-3) },
            new() { TaskId = trash.Id, OccurrenceDate = today.AddDays(-5), CompletedByMemberId = m.Eve.Id, CompletedAt = now.AddDays(-5) },
            // Dishwasher rotates between adults.
            new() { TaskId = dishwasher.Id, OccurrenceDate = today, CompletedByMemberId = m.Dan.Id, CompletedAt = now.AddHours(-3) },
            new() { TaskId = dishwasher.Id, OccurrenceDate = today.AddDays(-1), CompletedByMemberId = m.Eve.Id, CompletedAt = now.AddDays(-1).AddHours(-2) },
            new() { TaskId = dishwasher.Id, OccurrenceDate = today.AddDays(-2), CompletedByMemberId = m.Dan.Id, CompletedAt = now.AddDays(-2) },
            new() { TaskId = dishwasher.Id, OccurrenceDate = today.AddDays(-4), CompletedByMemberId = m.Eve.Id, CompletedAt = now.AddDays(-4) },
            // Plants — Eve watered last Saturday.
            new() { TaskId = plants.Id, OccurrenceDate = LastWeekday(today, DayOfWeek.Saturday).AddDays(-7), CompletedByMemberId = m.Eve.Id, CompletedAt = now.AddDays(-8) },
            // Bathroom — Dan two Sundays ago.
            new() { TaskId = bathroom.Id, OccurrenceDate = LastWeekday(today, DayOfWeek.Sunday).AddDays(-7), CompletedByMemberId = m.Dan.Id, CompletedAt = now.AddDays(-10) },
            // Rent — Dan paid this month.
            new() { TaskId = rent.Id, OccurrenceDate = new DateOnly(today.Year, today.Month, 1), CompletedByMemberId = m.Dan.Id, CompletedAt = now.AddDays(-today.Day + 1) },
            // Kid helping out.
            new() { TaskId = trash.Id, OccurrenceDate = today.AddDays(-7), CompletedByMemberId = m.Alice.Id, CompletedAt = now.AddDays(-7) },
            new() { TaskId = trash.Id, OccurrenceDate = today.AddDays(-10), CompletedByMemberId = m.Bob.Id, CompletedAt = now.AddDays(-10) },
        };
        db.TaskCompletions.AddRange(completions);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static DateOnly LastWeekday(DateOnly anchor, DayOfWeek target)
    {
        var diff = ((int)anchor.DayOfWeek - (int)target + 7) % 7;
        return diff == 0 ? anchor : anchor.AddDays(-diff);
    }

    private static async Task SeedWallAsync(
        ApplicationDbContext db,
        MarkdownRenderer markdown,
        Family family,
        DemoMembers m,
        CancellationToken cancellationToken)
    {
        var memberCandidates = new List<MentionCandidate>
        {
            new(m.Dan.Id, m.Dan.DisplayName),
            new(m.Eve.Id, m.Eve.DisplayName),
            new(m.Alice.Id, m.Alice.DisplayName),
            new(m.Bob.Id, m.Bob.DisplayName),
        };

        var now = DateTimeOffset.UtcNow;

        var post1Source = "Grandma's coming over for dinner on Saturday — any dietary requests?";
        var post1 = await CreateWallMessageAsync(
            db, markdown, family, m.Dan, post1Source, isPinned: false,
            createdAt: now.AddDays(-3), memberCandidates, cancellationToken);
        await AddCommentAsync(db, markdown, post1, m.Eve,
            "Maybe skip the cilantro this time, Alice doesn't love it 🌿",
            now.AddDays(-3).AddHours(2), memberCandidates, cancellationToken);
        await AddCommentAsync(db, markdown, post1, m.Alice,
            "And lots of dessert please 🍰",
            now.AddDays(-2).AddHours(-4), memberCandidates, cancellationToken);
        AddReactions(db, post1, new[]
        {
            (m.Eve.Id, "❤️"),
            (m.Alice.Id, "🎉"),
            (m.Bob.Id, "🎉"),
        }, now);

        var post2Source = """
            **🛒 Shopping list this week**

            - Milk, eggs, sourdough
            - Bananas, apples, lentils
            - Olive oil, tomato sauce
            - Toilet paper (we're almost out)
            """;
        var post2 = await CreateWallMessageAsync(
            db, markdown, family, m.Eve, post2Source, isPinned: true,
            createdAt: now.AddDays(-2), memberCandidates, cancellationToken);
        AddReactions(db, post2, new[]
        {
            (m.Dan.Id, "👍"),
        }, now);

        var post3Source = "Alice's ballet recital is on the **30th**! 🩰 Mark your calendars.";
        var post3 = await CreateWallMessageAsync(
            db, markdown, family, m.Eve, post3Source, isPinned: false,
            createdAt: now.AddDays(-1), memberCandidates, cancellationToken);
        AddReactions(db, post3, new[]
        {
            (m.Dan.Id, "🎉"),
            (m.Alice.Id, "❤️"),
        }, now);

        var post4Source = "Anybody up for bouldering tomorrow afternoon? Could rope Bob into trying 🧗";
        var post4 = await CreateWallMessageAsync(
            db, markdown, family, m.Dan, post4Source, isPinned: false,
            createdAt: now.AddHours(-5), memberCandidates, cancellationToken);
        await AddCommentAsync(db, markdown, post4, m.Eve,
            "I'm in 💪",
            now.AddHours(-2), memberCandidates, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<WallMessage> CreateWallMessageAsync(
        ApplicationDbContext db,
        MarkdownRenderer markdown,
        Family family,
        FamilyMember author,
        string source,
        bool isPinned,
        DateTimeOffset createdAt,
        IReadOnlyList<MentionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var rendered = markdown.RenderWithMentions(source, candidates);
        var message = new WallMessage
        {
            FamilyId = family.Id,
            AuthorMemberId = author.Id,
            Text = rendered.Markdown,
            TextHtml = rendered.Html,
            IsPinned = isPinned,
            PinnedAt = isPinned ? createdAt : null,
        };
        db.WallMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        // The DbContext audit-stamping overrides CreatedAt to "now" on insert,
        // which would collapse every demo post into the same instant and ruin
        // the chronological feed. Back-date in a second roundtrip that skips
        // the SaveChanges pipeline so the screenshots show a credible spread.
        await db.WallMessages
            .Where(m => m.Id == message.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.CreatedAt, createdAt)
                .SetProperty(m => m.UpdatedAt, (DateTimeOffset?)null),
                cancellationToken);
        return message;
    }

    private static async Task AddCommentAsync(
        ApplicationDbContext db,
        MarkdownRenderer markdown,
        WallMessage message,
        FamilyMember author,
        string source,
        DateTimeOffset createdAt,
        IReadOnlyList<MentionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var rendered = markdown.RenderWithMentions(source, candidates);
        var comment = new WallComment
        {
            MessageId = message.Id,
            AuthorMemberId = author.Id,
            Text = rendered.Markdown,
            TextHtml = rendered.Html,
        };
        db.WallComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        // Same backdating dance as wall messages — comments are ordered by
        // CreatedAt under their parent message, and we want each to land
        // visibly after the post it replies to.
        await db.WallComments
            .Where(c => c.Id == comment.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.CreatedAt, createdAt)
                .SetProperty(c => c.UpdatedAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    private static void AddReactions(
        ApplicationDbContext db,
        WallMessage message,
        IEnumerable<(Guid MemberId, string Emoji)> reactions,
        DateTimeOffset at)
    {
        foreach (var (memberId, emoji) in reactions)
        {
            db.WallReactions.Add(new WallReaction
            {
                MessageId = message.Id,
                MemberId = memberId,
                Emoji = emoji,
                ReactedAt = at,
            });
        }
    }

    private static async Task SeedMealsAsync(
        ApplicationDbContext db,
        Family family,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // Anchor the meal plan to the Monday of the current week so the
        // screenshots always show a full Mon-Sun grid no matter the weekday.
        var monday = LastWeekday(today, DayOfWeek.Monday);
        var slots = new[]
        {
            (monday.AddDays(0), MealSlot.Lunch, "Tomato salad", "Pasta carbonara"),
            (monday.AddDays(0), MealSlot.Dinner, "Vegetable soup", "Grilled cheese"),
            (monday.AddDays(1), MealSlot.Lunch, (string?)null, "Lentil stew"),
            (monday.AddDays(1), MealSlot.Dinner, (string?)null, "Sushi takeout 🍣"),
            (monday.AddDays(2), MealSlot.Lunch, "Caesar salad", "Roast chicken & potatoes"),
            (monday.AddDays(2), MealSlot.Dinner, (string?)null, "Veggie omelette"),
            (monday.AddDays(3), MealSlot.Lunch, (string?)null, "Beef tacos"),
            (monday.AddDays(3), MealSlot.Dinner, "Greek yoghurt", "Buddha bowl"),
            (monday.AddDays(4), MealSlot.Lunch, "Bruschetta", "Margherita pizza 🍕"),
            (monday.AddDays(4), MealSlot.Dinner, (string?)null, "Leftovers"),
            (monday.AddDays(5), MealSlot.Lunch, "Gazpacho", "Grandma's paella"),
            (monday.AddDays(5), MealSlot.Dinner, (string?)null, "Cheese & charcuterie"),
            (monday.AddDays(6), MealSlot.Lunch, (string?)null, "Roast lamb"),
            (monday.AddDays(6), MealSlot.Dinner, (string?)null, "Tomato & basil soup"),
        };

        foreach (var (date, slot, first, second) in slots)
        {
            db.MealPlanSlots.Add(new MealPlanSlot
            {
                FamilyId = family.Id,
                Date = date,
                Slot = slot,
                FirstCourse = first,
                SecondCourse = second,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSchoolAsync(
        ApplicationDbContext db,
        Family family,
        DemoMembers m,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        db.SchoolProfiles.AddRange(
            new SchoolProfile
            {
                FamilyMemberId = m.Alice.Id,
                SchoolName = "Lincoln Elementary",
                Grade = "3rd grade",
                Tutor = "Ms. Johnson",
                TransportMode = TransportMode.Walk,
                MorningTime = new TimeOnly(8, 30),
                AfternoonTime = new TimeOnly(16, 0),
            },
            new SchoolProfile
            {
                FamilyMemberId = m.Bob.Id,
                SchoolName = "Lincoln Elementary",
                Grade = "Kindergarten",
                Tutor = "Mr. Lopez",
                TransportMode = TransportMode.Walk,
                MorningTime = new TimeOnly(8, 30),
                AfternoonTime = new TimeOnly(13, 0),
            });

        // Weekly drop-off / pick-up routine, Mon-Fri. Dan walks them in, Eve
        // picks them up — credible distribution that also shows both adults.
        foreach (var dow in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            db.SchoolDaySchedules.Add(new SchoolDaySchedule
            {
                FamilyMemberId = m.Alice.Id,
                DayOfWeek = dow,
                DropoffMemberId = m.Dan.Id,
                PickupMemberId = m.Eve.Id,
            });
            db.SchoolDaySchedules.Add(new SchoolDaySchedule
            {
                FamilyMemberId = m.Bob.Id,
                DayOfWeek = dow,
                DropoffMemberId = m.Dan.Id,
                PickupMemberId = m.Eve.Id,
            });
        }

        // One extracurricular per kid so the school overview has something
        // visible without overwhelming the screenshot.
        db.Extracurriculars.AddRange(
            new Extracurricular
            {
                FamilyId = family.Id,
                FamilyMemberId = m.Alice.Id,
                Name = "Ballet",
                Location = "Lincoln Dance Studio",
                WeeklyDays = DayOfWeekMask.Tuesday | DayOfWeekMask.Thursday,
                StartTime = new TimeOnly(17, 0),
                EndTime = new TimeOnly(18, 0),
                StartDate = today.AddMonths(-2),
                DefaultDropoffMemberId = m.Eve.Id,
                DefaultPickupMemberId = m.Eve.Id,
            },
            new Extracurricular
            {
                FamilyId = family.Id,
                FamilyMemberId = m.Bob.Id,
                Name = "Swimming",
                Location = "Community Pool",
                WeeklyDays = DayOfWeekMask.Wednesday,
                StartTime = new TimeOnly(17, 30),
                EndTime = new TimeOnly(18, 30),
                StartDate = today.AddMonths(-2),
                DefaultDropoffMemberId = m.Dan.Id,
                DefaultPickupMemberId = m.Dan.Id,
            });

        // One holiday in the near future to populate the "next break" UI.
        db.SchoolHolidays.Add(new SchoolHoliday
        {
            FamilyId = family.Id,
            Label = "Spring break",
            StartDate = today.AddDays(21),
            EndDate = today.AddDays(28),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAgendaAsync(
        ApplicationDbContext db,
        Family family,
        DemoMembers m,
        CancellationToken cancellationToken)
    {
        // Weekly "who's where" patterns so the dashboard agenda widget has
        // signal. Kept minimal: working hours for the adults plus one evening
        // activity each.
        var workdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var dow in workdays)
        {
            db.MemberAgendaPatterns.Add(new MemberAgendaPattern
            {
                FamilyId = family.Id,
                FamilyMemberId = m.Dan.Id,
                DayOfWeek = dow,
                Label = "Office",
                Location = "Downtown",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(18, 0),
                TransportMode = AgendaTransportMode.Bus,
                IsAway = true,
            });
        }
        foreach (var dow in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday })
        {
            db.MemberAgendaPatterns.Add(new MemberAgendaPattern
            {
                FamilyId = family.Id,
                FamilyMemberId = m.Eve.Id,
                DayOfWeek = dow,
                Label = "Studio",
                Location = "Old town",
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(19, 0),
                TransportMode = AgendaTransportMode.Other,
                IsAway = true,
            });
        }
        db.MemberAgendaPatterns.Add(new MemberAgendaPattern
        {
            FamilyId = family.Id,
            FamilyMemberId = m.Eve.Id,
            DayOfWeek = DayOfWeek.Wednesday,
            Label = "Pilates",
            Location = "Wellness Center",
            StartTime = new TimeOnly(19, 30),
            EndTime = new TimeOnly(20, 30),
            TransportMode = AgendaTransportMode.Walk,
            IsAway = true,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Bundle of seeded members so the rest of the seeder can refer to
    /// them by role without juggling four parameters everywhere.</summary>
    private sealed record DemoMembers(FamilyMember Dan, FamilyMember Eve, FamilyMember Alice, FamilyMember Bob);
}
