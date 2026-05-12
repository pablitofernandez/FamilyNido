using FamilyNido.Domain.HouseholdTasks;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>
/// Projection of a single task occurrence (scheduled date + optional completion).
/// Used by the "today"/"week" list views and returned from the complete/undo endpoints.
/// </summary>
/// <param name="TaskId">Owning task.</param>
/// <param name="OccurrenceDate">Date this occurrence belongs to.</param>
/// <param name="IsCompleted">True when a <see cref="TaskCompletion"/> row exists for this date.</param>
/// <param name="CompletedByMemberId">Member who marked it done, if any.</param>
/// <param name="CompletedAt">When the completion was recorded.</param>
/// <param name="Note">Optional note left by the completer.</param>
public sealed record TaskOccurrenceDto(
    Guid TaskId,
    DateOnly OccurrenceDate,
    bool IsCompleted,
    Guid? CompletedByMemberId,
    DateTimeOffset? CompletedAt,
    string? Note)
{
    /// <summary>Build an occurrence DTO from a task + optional completion row.</summary>
    public static TaskOccurrenceDto From(HouseholdTask task, DateOnly date, TaskCompletion? completion) =>
        completion is null
            ? new TaskOccurrenceDto(task.Id, date, false, null, null, null)
            : new TaskOccurrenceDto(
                task.Id,
                completion.OccurrenceDate,
                true,
                completion.CompletedByMemberId,
                completion.CompletedAt,
                completion.Note);
}
