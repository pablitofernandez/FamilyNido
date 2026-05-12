using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Wall;

/// <summary>
/// A single emoji reaction to a <see cref="WallMessage"/>. The unique constraint
/// <c>(MessageId, MemberId, Emoji)</c> allows a member to reacted with several
/// distinct emojis to the same message but never the same emoji twice, so toggling
/// behaviour stays idempotent. Not an <c>AuditableEntity</c>: reactions are
/// throw-away records, we only keep the instant they were placed.
/// </summary>
public sealed class WallReaction
{
    /// <summary>Surrogate PK. UUIDv7 keeps inserts time-ordered.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Message this reaction belongs to.</summary>
    public required Guid MessageId { get; set; }

    /// <summary>Navigation to the owning <see cref="WallMessage"/>.</summary>
    public WallMessage? Message { get; set; }

    /// <summary>Member who reacted.</summary>
    public required Guid MemberId { get; set; }

    /// <summary>Navigation to the reacting <see cref="FamilyMember"/>.</summary>
    public FamilyMember? Member { get; set; }

    /// <summary>The emoji as a string (e.g. <c>"❤️"</c>, <c>"🎉"</c>).</summary>
    public required string Emoji { get; set; }

    /// <summary>UTC instant the reaction was placed.</summary>
    public required DateTimeOffset ReactedAt { get; set; }
}
