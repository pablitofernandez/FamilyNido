using FamilyNido.Domain.HouseholdTasks;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>
/// Read-model projection of a <see cref="HouseholdTask"/> returned by the API.
/// </summary>
/// <param name="Id">Stable task identifier.</param>
/// <param name="Title">Short title shown in the list.</param>
/// <param name="Description">Optional longer description.</param>
/// <param name="Category">Free-form category label.</param>
/// <param name="Recurrence">Recurrence mode.</param>
/// <param name="WeeklyDays">Bitmask of weekdays when <paramref name="Recurrence"/> is Weekly.</param>
/// <param name="MonthlyDay">Day of the month (1–31 or -1 for "last day") when <paramref name="Recurrence"/> is Monthly.</param>
/// <param name="TimeOfDay">Informative time-of-day target, if any.</param>
/// <param name="StartDate">Pivot date: no occurrences are generated before this date.</param>
/// <param name="DueDate">Target date for single-shot tasks.</param>
/// <param name="ResponsibleMemberId">The single member who executes the task (null = open, anyone can do it).</param>
/// <param name="RelatedMemberIds">Ids of the members the task concerns (zero, one, or many).</param>
/// <param name="IsArchived">Whether the task is archived.</param>
/// <param name="IsFloating">When true the task has no fixed date and stays pending in "Hoy" until completed once.</param>
/// <param name="CreatedByMemberId">Member who created the task; only they or an admin can delete it.</param>
/// <param name="CreatedAt">UTC instant of creation.</param>
/// <param name="Points">Reward (1..10) earned by whoever marks an occurrence as done.</param>
/// <param name="LatestCompletion">Most recent completion of the task, or null if never completed.</param>
public sealed record HouseholdTaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    RecurrenceMode Recurrence,
    DayOfWeekMask? WeeklyDays,
    int? MonthlyDay,
    TimeOnly? TimeOfDay,
    DateOnly StartDate,
    DateOnly? DueDate,
    Guid? ResponsibleMemberId,
    IReadOnlyList<Guid> RelatedMemberIds,
    bool IsArchived,
    bool IsFloating,
    Guid CreatedByMemberId,
    DateTimeOffset CreatedAt,
    int Points,
    LatestCompletionDto? LatestCompletion)
{
    /// <summary>Project a domain entity to the DTO shape used by the API.</summary>
    public static HouseholdTaskDto From(HouseholdTask t, LatestCompletionDto? latest = null) => new(
        Id: t.Id,
        Title: t.Title,
        Description: t.Description,
        Category: t.Category,
        Recurrence: t.Recurrence,
        WeeklyDays: t.WeeklyDays,
        MonthlyDay: t.MonthlyDay,
        TimeOfDay: t.TimeOfDay,
        StartDate: t.StartDate,
        DueDate: t.DueDate,
        ResponsibleMemberId: t.ResponsibleMemberId,
        RelatedMemberIds: [.. t.RelatedMembers.Select(a => a.Id)],
        IsArchived: t.IsArchived,
        IsFloating: t.IsFloating,
        CreatedByMemberId: t.CreatedByMemberId,
        CreatedAt: t.CreatedAt,
        Points: t.Points,
        LatestCompletion: latest);
}

/// <summary>Compact view of the most recent completion of a task.</summary>
/// <param name="OccurrenceDate">Date of the occurrence the member ticked.</param>
/// <param name="CompletedByMemberId">Member who completed it (null if the row is anonymised).</param>
/// <param name="CompletedAt">UTC instant the completion was registered.</param>
public sealed record LatestCompletionDto(
    DateOnly OccurrenceDate,
    Guid? CompletedByMemberId,
    DateTimeOffset CompletedAt);
