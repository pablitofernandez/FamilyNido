using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>GET /api/school/overview?from=YYYY-MM-DD&amp;to=YYYY-MM-DD</c>.
/// Returns the persisted weekly schedule, all per-date exceptions, all
/// holidays intersecting the range, plus a day-by-day "resolved" view that
/// merges those layers in priority order: holiday &gt; exception &gt; schedule.
/// </summary>
public static class GetSchoolOverview
{
    /// <summary>Query carrying the inclusive [From, To] range.</summary>
    public sealed record Query(DateOnly From, DateOnly To) : IRequest<Result<SchoolOverviewDto>>;

    /// <summary>Composes the overview DTO.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<SchoolOverviewDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext)
        {
            _db = db;
            _userContext = userContext;
        }

        /// <inheritdoc />
        public async Task<Result<SchoolOverviewDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            if (request.To < request.From)
            {
                return ApplicationError.Validation("school.overview.bad_range", "End date must be on or after the start date.");
            }

            var familyId = current.Family.Id;

            // ── Persisted weekly schedule per kid (drop-off + pick-up).
            var scheduleRows = await _db.SchoolDaySchedules
                .AsNoTracking()
                .Include(s => s.FamilyMember)
                .Where(s => s.FamilyMember!.FamilyId == familyId)
                .Select(s => new { s.FamilyMemberId, s.DayOfWeek, s.DropoffMemberId, s.PickupMemberId })
                .ToListAsync(cancellationToken);

            var scheduleByKid = scheduleRows
                .GroupBy(r => r.FamilyMemberId)
                .Select(g => new KidScheduleDto(
                    g.Key,
                    g.OrderBy(r => r.DayOfWeek)
                     .Select(r => new SchoolDayScheduleSlotDto(r.DayOfWeek, r.DropoffMemberId, r.PickupMemberId))
                     .ToList()))
                .ToList();

            // Per-(kid, weekday) lookup for the resolution loop below.
            var scheduleLookup = scheduleRows
                .ToDictionary(
                    r => (r.FamilyMemberId, r.DayOfWeek),
                    r => (r.DropoffMemberId, r.PickupMemberId));

            // ── Per-date exceptions in range.
            var exceptions = await _db.SchoolDayExceptions
                .AsNoTracking()
                .Where(e => e.FamilyId == familyId && e.Date >= request.From && e.Date <= request.To)
                .Select(e => new SchoolDayExceptionDto(
                    e.Id,
                    e.FamilyMemberId,
                    e.Date,
                    e.IsCancelled,
                    e.DropoffMemberId,
                    e.PickupMemberId,
                    e.MorningTime,
                    e.AfternoonTime,
                    e.Notes))
                .ToListAsync(cancellationToken);

            var exceptionLookup = exceptions
                .ToDictionary(e => (e.MemberId, e.Date));

            // ── Holidays that intersect the range.
            var holidays = await _db.SchoolHolidays
                .AsNoTracking()
                .Where(h => h.FamilyId == familyId && h.StartDate <= request.To && h.EndDate >= request.From)
                .OrderBy(h => h.StartDate)
                .Select(h => new SchoolHolidayDto(h.Id, h.StartDate, h.EndDate, h.Label))
                .ToListAsync(cancellationToken);

            // Per-kid profile bits we need for resolution: transport mode + the
            // default morning / afternoon times that exception overrides fall
            // back to. Defaults to None / null times when the kid has no profile.
            var profileByKid = await _db.SchoolProfiles
                .AsNoTracking()
                .Where(p => p.FamilyMember!.FamilyId == familyId)
                .Select(p => new { p.FamilyMemberId, p.TransportMode, p.MorningTime, p.AfternoonTime })
                .ToDictionaryAsync(
                    p => p.FamilyMemberId,
                    p => (p.TransportMode, p.MorningTime, p.AfternoonTime),
                    cancellationToken);

            Domain.School.TransportMode TransportFor(Guid kidId)
                => profileByKid.TryGetValue(kidId, out var p) ? p.TransportMode : Domain.School.TransportMode.None;
            (TimeOnly? Morning, TimeOnly? Afternoon) DefaultTimesFor(Guid kidId)
                => profileByKid.TryGetValue(kidId, out var p) ? (p.MorningTime, p.AfternoonTime) : (null, null);

            // ── Resolution loop: for every (kid that has any schedule row OR any
            // exception in range, day in range) emit a resolved row.
            var kidIds = scheduleRows.Select(r => r.FamilyMemberId)
                .Concat(exceptions.Select(e => e.MemberId))
                .Distinct()
                .ToList();

            var resolved = new List<ResolvedSchoolDayDto>();
            for (var date = request.From; date <= request.To; date = date.AddDays(1))
            {
                var holiday = holidays.FirstOrDefault(h => h.StartDate <= date && h.EndDate >= date);
                foreach (var kidId in kidIds)
                {
                    var hasException = exceptionLookup.TryGetValue((kidId, date), out var ex);
                    var hasSchedule = scheduleLookup.TryGetValue((kidId, date.DayOfWeek), out var fromSchedule);

                    // Skip cells that have nothing to say (no schedule, no override, no holiday match).
                    if (!hasException && !hasSchedule && holiday is null) continue;

                    var mode = TransportFor(kidId);
                    var defaults = DefaultTimesFor(kidId);
                    if (holiday is not null)
                    {
                        resolved.Add(new ResolvedSchoolDayDto(
                            kidId, date, mode,
                            null, null, null, null,
                            IsCancelled: true, holiday.Label, ex?.Notes));
                        continue;
                    }
                    if (hasException)
                    {
                        // Override semantics: if a slot is null in the exception we still
                        // fall back to the schedule for that slot — that lets you override
                        // just the pickup of a day without forcing the user to re-enter the
                        // morning drop-off. Same for times: exception time wins, otherwise
                        // we ship the profile defaults.
                        var exDropoff = ex!.DropoffMemberId ?? (hasSchedule ? fromSchedule.DropoffMemberId : null);
                        var exPickup = ex.PickupMemberId ?? (hasSchedule ? fromSchedule.PickupMemberId : null);
                        var exMorning = ex.MorningTime ?? defaults.Morning;
                        var exAfternoon = ex.AfternoonTime ?? defaults.Afternoon;
                        resolved.Add(new ResolvedSchoolDayDto(
                            kidId, date, mode,
                            ex.IsCancelled ? null : exDropoff,
                            ex.IsCancelled ? null : exPickup,
                            ex.IsCancelled ? null : exMorning,
                            ex.IsCancelled ? null : exAfternoon,
                            ex.IsCancelled, null, ex.Notes));
                        continue;
                    }
                    resolved.Add(new ResolvedSchoolDayDto(
                        kidId, date, mode,
                        fromSchedule.DropoffMemberId,
                        fromSchedule.PickupMemberId,
                        defaults.Morning,
                        defaults.Afternoon,
                        IsCancelled: false, null, null));
                }
            }

            // ── Extracurriculars active in the family + their exceptions in range.
            var extracurriculars = await _db.Extracurriculars
                .AsNoTracking()
                .Where(e => e.FamilyId == familyId && !e.IsArchived
                    && e.StartDate <= request.To
                    && (e.EndDate == null || e.EndDate >= request.From))
                .ToListAsync(cancellationToken);

            var extracurricularDtos = extracurriculars
                .Select(e => new ExtracurricularDto(
                    e.Id, e.FamilyMemberId, e.Name, e.Location, e.ContactPhone,
                    e.WeeklyDays, e.StartTime, e.EndTime, e.StartDate, e.EndDate,
                    e.DefaultDropoffMemberId, e.DefaultPickupMemberId, e.Notes, e.IsArchived))
                .ToList();

            var extracurricularIds = extracurriculars.Select(e => e.Id).ToList();
            var extraExceptions = extracurricularIds.Count == 0
                ? new List<ExtracurricularExceptionDto>()
                : await _db.ExtracurricularExceptions
                    .AsNoTracking()
                    .Where(x => extracurricularIds.Contains(x.ExtracurricularId)
                        && x.Date >= request.From && x.Date <= request.To)
                    .Select(x => new ExtracurricularExceptionDto(
                        x.Id, x.ExtracurricularId, x.Date, x.IsCancelled,
                        x.DropoffMemberId, x.PickupMemberId, x.Notes))
                    .ToListAsync(cancellationToken);

            var extraExceptionLookup = extraExceptions
                .ToDictionary(x => (x.ExtracurricularId, x.Date));

            // ── Resolution: each extracurricular yields one row per scheduled
            // weekday inside the requested range, then merges exception/holiday.
            var resolvedExtra = new List<ResolvedExtracurricularDto>();
            foreach (var activity in extracurriculars)
            {
                var floor = activity.StartDate > request.From ? activity.StartDate : request.From;
                var ceiling = activity.EndDate is { } end && end < request.To ? end : request.To;
                for (var date = floor; date <= ceiling; date = date.AddDays(1))
                {
                    if ((activity.WeeklyDays & ToMask(date.DayOfWeek)) == DayOfWeekMask.None) continue;

                    var holiday = holidays.FirstOrDefault(h => h.StartDate <= date && h.EndDate >= date);
                    var hasException = extraExceptionLookup.TryGetValue((activity.Id, date), out var ex);

                    if (holiday is not null)
                    {
                        resolvedExtra.Add(new ResolvedExtracurricularDto(
                            activity.Id, activity.FamilyMemberId, date,
                            activity.StartTime, activity.EndTime, activity.Name,
                            activity.Location, activity.ContactPhone,
                            null, null,
                            IsCancelled: true, holiday.Label, ex?.Notes));
                        continue;
                    }
                    if (hasException && ex!.IsCancelled)
                    {
                        resolvedExtra.Add(new ResolvedExtracurricularDto(
                            activity.Id, activity.FamilyMemberId, date,
                            activity.StartTime, activity.EndTime, activity.Name,
                            activity.Location, activity.ContactPhone,
                            null, null,
                            IsCancelled: true, null, ex.Notes));
                        continue;
                    }
                    var dropoff = (hasException ? ex!.DropoffMemberId : null) ?? activity.DefaultDropoffMemberId;
                    var pickup = (hasException ? ex!.PickupMemberId : null) ?? activity.DefaultPickupMemberId;
                    resolvedExtra.Add(new ResolvedExtracurricularDto(
                        activity.Id, activity.FamilyMemberId, date,
                        activity.StartTime, activity.EndTime, activity.Name,
                        activity.Location, activity.ContactPhone,
                        dropoff, pickup,
                        IsCancelled: false, null, hasException ? ex!.Notes : null));
                }
            }

            return new SchoolOverviewDto(
                request.From, request.To, scheduleByKid, exceptions, holidays, resolved,
                extracurricularDtos, extraExceptions, resolvedExtra);
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
    }
}
