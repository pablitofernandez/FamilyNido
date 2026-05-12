using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.HouseholdTasks;

/// <summary>
/// Shared household chore (fregar el baño, sacar la basura, comprar pan). Owns its own
/// recurrence motor — deliberately simpler than iCalendar RRULE — and its state is tracked
/// per-occurrence via <see cref="TaskCompletion"/>, keeping a full audit of who did what.
/// </summary>
public sealed class HouseholdTask : AuditableEntity
{
    /// <summary>Family this task belongs to.</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Short title shown in the list ("Fregar baño").</summary>
    public required string Title { get; set; }

    /// <summary>Optional longer description or notes.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Free-form category label ("General", "Cocina", "Baño", "Jardín"…). Kept as a string
    /// rather than an enum so families can evolve their taxonomy without schema changes.
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>Recurrence pattern. Determines which of <see cref="WeeklyDays"/>/<see cref="MonthlyDay"/> applies.</summary>
    public RecurrenceMode Recurrence { get; set; } = RecurrenceMode.None;

    /// <summary>
    /// Bitmask of weekdays when <see cref="Recurrence"/> is <see cref="RecurrenceMode.Weekly"/>.
    /// Ignored for other modes.
    /// </summary>
    public DayOfWeekMask? WeeklyDays { get; set; }

    /// <summary>
    /// Day of the month (1..31) when <see cref="Recurrence"/> is <see cref="RecurrenceMode.Monthly"/>,
    /// with the special value <c>-1</c> meaning "last day of the month". A value above the last
    /// day of a given month collapses to that month's last day (so 31 on February becomes 28/29).
    /// </summary>
    public int? MonthlyDay { get; set; }

    /// <summary>
    /// Informative time-of-day target ("por la mañana", "a las 20:00"). Not used by the motor in v1;
    /// reserved for v2 push reminders (RF-TASK-008).
    /// </summary>
    public TimeOnly? TimeOfDay { get; set; }

    /// <summary>
    /// Pivot date: the task does not generate occurrences before this date, regardless of pattern.
    /// For non-recurring tasks without a <see cref="DueDate"/>, the single occurrence falls on this date.
    /// </summary>
    public required DateOnly StartDate { get; set; }

    /// <summary>
    /// Target date for single-shot tasks (<see cref="RecurrenceMode.None"/>). Null for recurring tasks
    /// or for undated one-shots (the latter fall back to <see cref="StartDate"/> for occurrence placement).
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Reward in "puntos" earned by the member that marks an occurrence done. Range 1..10
    /// (validated at the application layer). Drives the family scoreboard / gamification —
    /// totals are computed at query time from <see cref="Completions"/> joined back to this
    /// field, so editing the reward also reshapes the historical score (mutable history is
    /// the deliberate trade-off).
    /// </summary>
    public int Points { get; set; } = 5;

    /// <summary>
    /// Soft-delete flag. Archived tasks disappear from the default views but preserve their
    /// <see cref="Completions"/> history and can be restored.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// "Floating" task — has no fixed date and stays pending in the Today view every day
    /// until the first <see cref="TaskCompletion"/> is recorded. Mutually exclusive with
    /// recurrence (<see cref="Recurrence"/> must be <see cref="RecurrenceMode.None"/>) and
    /// with a target date (<see cref="DueDate"/> must be null) — those are validated at the
    /// application layer when the field is set. Used for "encargar el jarabe en la
    /// farmacia" type chores: important to remember every day, not tied to one in particular.
    /// </summary>
    public bool IsFloating { get; set; }

    /// <summary>Member who created the task. Only that member (or an admin) may delete it.</summary>
    public required Guid CreatedByMemberId { get; set; }

    /// <summary>Navigation to the creator.</summary>
    public FamilyMember? CreatedByMember { get; set; }

    /// <summary>
    /// The single member that owns the execution of this task. Optional — a task with
    /// no responsible is "open" and any family member can mark it as done. Distinct
    /// from <see cref="RelatedMembers"/>: the responsible *does* the chore, while
    /// related members are the ones the chore is *about* (e.g. "go pick up the kids":
    /// responsible = Dan, related = Bob + Charlie).
    /// </summary>
    public Guid? ResponsibleMemberId { get; set; }

    /// <summary>Navigation to the responsible member.</summary>
    public FamilyMember? ResponsibleMember { get; set; }

    /// <summary>
    /// Members the task concerns or affects. Zero, one or many. Used to surface the
    /// task on the per-member dashboard even when the member is not the one doing it.
    /// </summary>
    public ICollection<FamilyMember> RelatedMembers { get; set; } = [];

    /// <summary>Per-occurrence completion records.</summary>
    public ICollection<TaskCompletion> Completions { get; set; } = [];

    /// <summary>
    /// Pure enumeration of the dates on which this task is scheduled to occur within
    /// <paramref name="from"/>..<paramref name="to"/> inclusive. Archived tasks yield no
    /// occurrences. The effective start is clamped to <see cref="StartDate"/>.
    /// </summary>
    /// <param name="from">Lower bound of the window (inclusive).</param>
    /// <param name="to">Upper bound of the window (inclusive).</param>
    /// <returns>Ordered sequence of occurrence dates.</returns>
    public IEnumerable<DateOnly> EnumerateOccurrences(DateOnly from, DateOnly to)
    {
        if (to < from || IsArchived)
        {
            yield break;
        }

        // Floating tasks have no scheduled date — they're emitted directly by
        // the application handlers on the "today" anchor when no completion
        // exists yet, so this method intentionally yields nothing for them.
        if (IsFloating)
        {
            yield break;
        }

        var start = from > StartDate ? from : StartDate;
        if (to < start)
        {
            yield break;
        }

        switch (Recurrence)
        {
            case RecurrenceMode.None:
                // Two flavours for non-recurring tasks:
                //   - Single-shot: DueDate equals StartDate, or DueDate is null.
                //     One occurrence on DueDate (or StartDate when undated).
                //   - "Deadline" task: DueDate > StartDate. The task appears every
                //     day in [StartDate, DueDate] so the family is reminded each
                //     day until it's completed. The application layer treats
                //     these like floating tasks once a completion exists (any
                //     completion at all wipes the task from future days), see
                //     <c>GetTodayTasks.LoadRangeAsync</c>.
                if (DueDate is { } due && due > StartDate)
                {
                    var rangeStart = start > StartDate ? start : StartDate;
                    var rangeEnd = to < due ? to : due;
                    for (var d = rangeStart; d <= rangeEnd; d = d.AddDays(1))
                    {
                        yield return d;
                    }
                }
                else
                {
                    var single = DueDate ?? StartDate;
                    if (single >= start && single <= to)
                    {
                        yield return single;
                    }
                }
                break;

            case RecurrenceMode.Daily:
                for (var d = start; d <= to; d = d.AddDays(1))
                {
                    yield return d;
                }
                break;

            case RecurrenceMode.Weekly:
                if (WeeklyDays is null or DayOfWeekMask.None)
                {
                    yield break;
                }
                for (var d = start; d <= to; d = d.AddDays(1))
                {
                    if ((WeeklyDays.Value & ToMask(d.DayOfWeek)) != DayOfWeekMask.None)
                    {
                        yield return d;
                    }
                }
                break;

            case RecurrenceMode.Monthly:
                if (MonthlyDay is null)
                {
                    yield break;
                }
                // Walk month-by-month to avoid iterating days that never match.
                var cursor = new DateOnly(start.Year, start.Month, 1);
                while (cursor <= to)
                {
                    var daysInMonth = DateTime.DaysInMonth(cursor.Year, cursor.Month);
                    var target = MonthlyDay.Value == -1
                        ? daysInMonth
                        : Math.Min(MonthlyDay.Value, daysInMonth);
                    var occurrence = new DateOnly(cursor.Year, cursor.Month, target);
                    if (occurrence >= start && occurrence <= to)
                    {
                        yield return occurrence;
                    }
                    cursor = cursor.AddMonths(1);
                }
                break;
        }
    }

    /// <summary>
    /// True when the task is scheduled to occur on <paramref name="date"/>. Convenience wrapper
    /// around <see cref="EnumerateOccurrences"/> used by handlers to validate that a given
    /// occurrence date is actually one of the task's occurrences.
    /// </summary>
    public bool HasOccurrenceOn(DateOnly date) => EnumerateOccurrences(date, date).Any();

    /// <summary>
    /// True when this is a "deadline" task — non-recurring with <see cref="DueDate"/> strictly
    /// after <see cref="StartDate"/>. These appear every day in the range until a single
    /// completion graduates them like a floating task.
    /// </summary>
    public bool IsDeadlineRange =>
        Recurrence == RecurrenceMode.None && DueDate is { } due && due > StartDate;

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
