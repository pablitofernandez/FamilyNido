using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.HouseholdTasks;

/// <summary>
/// A single "done" marker for one specific occurrence of a <see cref="HouseholdTask"/>.
/// The unique constraint <c>(TaskId, OccurrenceDate)</c> guarantees that marking the same
/// occurrence twice is idempotent. Preserving per-occurrence rows keeps an audit trail
/// (who did what, when) and feeds future statistics — streaks, member contributions.
/// </summary>
public sealed class TaskCompletion
{
    /// <summary>Surrogate PK, UUIDv7 for time-ordered inserts.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Owning task.</summary>
    public required Guid TaskId { get; set; }

    /// <summary>Navigation to the owning <see cref="HouseholdTask"/>.</summary>
    public HouseholdTask? Task { get; set; }

    /// <summary>
    /// Date the completion refers to. For recurring tasks this is the date of the specific
    /// occurrence being marked; for single-shot tasks it is the task's <see cref="HouseholdTask.DueDate"/>
    /// (or <see cref="DateOnly.MinValue"/> as a stand-in when there is no due date — see
    /// <see cref="HouseholdTask.HasOccurrenceOn"/>).
    /// </summary>
    public required DateOnly OccurrenceDate { get; set; }

    /// <summary>
    /// Member who marked the occurrence as done. Null means "anonymous" (e.g. the member
    /// was deleted after completing); we use <c>SET NULL</c> on delete to preserve history.
    /// </summary>
    public Guid? CompletedByMemberId { get; set; }

    /// <summary>Navigation to the completing <see cref="FamilyMember"/>.</summary>
    public FamilyMember? CompletedBy { get; set; }

    /// <summary>UTC instant the occurrence was marked.</summary>
    public required DateTimeOffset CompletedAt { get; set; }

    /// <summary>Optional free-text note left by the person marking the task (e.g. "hecho antes de comer").</summary>
    public string? Note { get; set; }
}
