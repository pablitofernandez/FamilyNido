using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Integrations;

/// <summary>
/// Long-lived shared secret used by an external integration (
/// shortcuts, scripts) to call FamilyNido endpoints under
/// <c>/api/integrations/**</c>. The plaintext token is shown once at creation
/// and only its SHA-256 digest is persisted, so a stolen database row cannot
/// be replayed against the API.
/// </summary>
/// <remarks>
/// Scope is limited to <c>/api/integrations/**</c>: a leaked token cannot read
/// the wall, the calendar, or anything else — at worst it can create the kind
/// of items the integration endpoints already accept (e.g. household tasks).
/// </remarks>
public sealed class IntegrationApiKey : AuditableEntity
{
    /// <summary>Family the token belongs to. Drives data isolation.</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>
    /// Member that created the token; recorded as the author of any artefact
    /// produced through it (e.g. <c>HouseholdTask.CreatedByMemberId</c>).
    /// </summary>
    public required Guid AuthorMemberId { get; set; }

    /// <summary>Navigation to the author member.</summary>
    public FamilyMember? AuthorMember { get; set; }

    /// <summary>Free-form human label ("my automation", "iPhone shortcuts").</summary>
    public required string Name { get; set; }

    /// <summary>Hex-encoded SHA-256 of the plaintext token. Lookups go through this.</summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// Public prefix shown in the UI so users can tell tokens apart without
    /// exposing the secret. Convention: first 12 chars of the plaintext, e.g.
    /// <c>bxn_a1b2c3d4</c>.
    /// </summary>
    public required string Prefix { get; set; }

    /// <summary>UTC instant of the most recent successful authentication. Null until first use.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>UTC instant when the token was revoked. Null while active. Soft-delete: row stays for audit.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>True when the token can still authenticate.</summary>
    public bool IsActive => RevokedAt is null;
}
