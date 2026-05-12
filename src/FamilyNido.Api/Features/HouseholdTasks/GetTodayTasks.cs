using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: tasks scheduled today, with their completion state (RF-TASK-006).</summary>
public static class GetTodayTasks
{
    /// <summary>Parameterless query — the date is derived from the family's time zone.</summary>
    public sealed record Query : IRequest<Result<DayTasksDto>>;

    /// <summary>Handler that loads tasks, expands occurrences and cross-references completions.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<DayTasksDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _clock;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext, TimeProvider clock)
        {
            _db = db;
            _userContext = userContext;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<Result<DayTasksDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var today = TodayInFamilyZone(_clock, current.Family.TimeZone);
            var day = await LoadRangeAsync(_db, current.Family.Id, today, today, today, cancellationToken);

            return day.Count > 0
                ? day[0]
                : new DayTasksDto(today, []);
        }

        /// <summary>
        /// Resolve "today" using the family's configured IANA time zone. Falls back to UTC
        /// when the zone is unknown — deployment issue we don't want to crash over. Shared
        /// with <see cref="GetWeekTasks"/>.
        /// </summary>
        internal static DateOnly TodayInFamilyZone(TimeProvider clock, string ianaZone)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaZone);
                var localNow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz);
                return DateOnly.FromDateTime(localNow.DateTime);
            }
            catch (TimeZoneNotFoundException)
            {
                return DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            }
        }

        /// <summary>
        /// Shared loader used by <see cref="GetTodayTasks"/> and <see cref="GetWeekTasks"/>:
        /// pulls non-archived tasks + their completions in the window, then expands
        /// occurrences in-memory via <see cref="Domain.HouseholdTasks.HouseholdTask.EnumerateOccurrences"/>.
        /// </summary>
        /// <remarks>
        /// Floating tasks live outside the recurrence engine: they appear on the
        /// <paramref name="todayAnchor"/> date (when present) for as long as no
        /// completion exists. Pass <c>null</c> to skip them entirely (e.g. when
        /// rendering a past week that never includes "today").
        /// </remarks>
        internal static async Task<List<DayTasksDto>> LoadRangeAsync(
            ApplicationDbContext db,
            Guid familyId,
            DateOnly from,
            DateOnly to,
            DateOnly? todayAnchor,
            CancellationToken ct)
        {
            var tasks = await db.HouseholdTasks
                .AsNoTracking()
                .Include(t => t.RelatedMembers)
                .Include(t => t.Completions.Where(c => c.OccurrenceDate >= from && c.OccurrenceDate <= to))
                .Where(t => t.FamilyId == familyId && !t.IsArchived)
                .ToListAsync(ct);

            // Floating + deadline-range tasks need a *global* completion check (any
            // completion at all marks them "done forever"), but we only loaded
            // completions inside the window above. Run a focused side query for
            // both flavours in one shot.
            var globalCheckTaskIds = tasks
                .Where(t => t.IsFloating || t.IsDeadlineRange)
                .Select(t => t.Id)
                .ToList();
            HashSet<Guid> globallyDone;
            if (globalCheckTaskIds.Count == 0)
            {
                globallyDone = [];
            }
            else
            {
                var ids = await db.TaskCompletions
                    .AsNoTracking()
                    .Where(c => globalCheckTaskIds.Contains(c.TaskId))
                    .Select(c => c.TaskId)
                    .Distinct()
                    .ToListAsync(ct);
                globallyDone = ids.ToHashSet();
            }

            var days = new List<DayTasksDto>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var items = new List<TaskOnDateDto>();
                foreach (var task in tasks)
                {
                    if (task.IsFloating)
                    {
                        // Skip floating tasks already completed at any point — they
                        // graduate from the pending list forever.
                        if (globallyDone.Contains(task.Id)) continue;
                        // Surface them on the "today" anchor only so the week view
                        // doesn't repeat the same row 7 times.
                        if (todayAnchor is null || d != todayAnchor.Value) continue;

                        items.Add(new TaskOnDateDto(
                            HouseholdTaskDto.From(task),
                            TaskOccurrenceDto.From(task, d, completion: null)));
                        continue;
                    }

                    // Deadline-range tasks (non-recurring, StartDate < DueDate) appear
                    // every day in their window unless any completion already exists
                    // anywhere — same "done forever" rule as floating tasks.
                    if (task.IsDeadlineRange && globallyDone.Contains(task.Id))
                    {
                        continue;
                    }

                    if (!task.HasOccurrenceOn(d))
                    {
                        continue;
                    }

                    var completion = task.Completions.FirstOrDefault(c => c.OccurrenceDate == d);
                    items.Add(new TaskOnDateDto(
                        HouseholdTaskDto.From(task),
                        TaskOccurrenceDto.From(task, d, completion)));
                }
                days.Add(new DayTasksDto(d, items));
            }
            return days;
        }
    }
}
