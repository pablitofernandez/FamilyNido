using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Identity;

/// <summary>
/// One-time, time-limited token an admin emits so a person can claim a
/// <see cref="FamilyMember"/>. The recipient may accept the invitation
/// indistinctly via OIDC (existing PocketID account) or by setting a local
/// password — both paths bind the same <see cref="FamilyMember"/> to a
/// <see cref="User"/> and consume the row.
/// </summary>
/// <remarks>
/// Only the SHA-256 of the token is persisted (<see cref="TokenHash"/>); the
/// raw token lives only in the email sent to the recipient and in the URL
/// the recipient clicks. Acceptance is performed atomically with a
/// conditional UPDATE so two concurrent clicks cannot consume the same
/// invitation twice.
/// </remarks>
public sealed class Invitation : AuditableEntity
{
    /// <summary>Family this invitation belongs to.</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Member that will be linked to the accepting user.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the family member.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Email the invitation was sent to (normalized: trimmed + lowercase).</summary>
    public required string Email { get; set; }

    /// <summary>Role the accepting user receives. Validator forbids <see cref="FamilyRole.Guest"/>.</summary>
    public required FamilyRole RoleOnAccept { get; set; }

    /// <summary>SHA-256 of the raw token. Indexed UNIQUE.</summary>
    public required byte[] TokenHash { get; set; }

    /// <summary>UTC instant after which the token can no longer be redeemed.</summary>
    public required DateTimeOffset ExpiresAt { get; set; }

    /// <summary>UTC instant the token was redeemed. Null while pending.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>User that redeemed the token. Null while pending.</summary>
    public Guid? ConsumedByUserId { get; set; }

    /// <summary>UTC instant the token was revoked by an admin. Null when active.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
