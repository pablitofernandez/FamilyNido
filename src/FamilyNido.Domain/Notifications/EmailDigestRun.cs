using FamilyNido.Domain.Identity;

namespace FamilyNido.Domain.Notifications;

/// <summary>
/// Audit row marking that today's digest has been processed for a given user.
/// The composite primary key <c>(UserId, LocalDate)</c> doubles as a uniqueness
/// guard so the background scanner can never double-send: inserting the row
/// before queueing the email means a second pass within the same local day is
/// short-circuited by EF.
/// </summary>
public sealed class EmailDigestRun
{
    /// <summary>User the digest was processed for.</summary>
    public required Guid UserId { get; set; }

    /// <summary>Navigation to the owning <see cref="User"/>.</summary>
    public User? User { get; set; }

    /// <summary>Date in the family's local timezone — the natural deduplication key.</summary>
    public required DateOnly LocalDate { get; set; }

    /// <summary>UTC instant the row was inserted (and the email queued, if applicable).</summary>
    public DateTimeOffset SentAt { get; set; }
}
