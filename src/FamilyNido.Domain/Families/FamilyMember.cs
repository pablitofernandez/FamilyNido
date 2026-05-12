using FamilyNido.Domain.Common;
using FamilyNido.Domain.Identity;

namespace FamilyNido.Domain.Families;

/// <summary>
/// A person belonging to the family. Central aggregate that other modules (calendar,
/// health, school…) point at as the "owner" of their items.
/// </summary>
/// <remarks>
/// A member may or may not have an associated <see cref="User"/> account:
/// only adults who actually log in are linked. Children and reference-only members
/// (grandparents, caretakers) stay as <see cref="FamilyMember"/> rows without a user.
/// </remarks>
public sealed class FamilyMember : AuditableEntity
{
    /// <summary>Family this member belongs to.</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Display name shown across the UI ("Dan", "Bob", etc.).</summary>
    public required string DisplayName { get; set; }

    /// <summary>Date of birth if known. Drives age-derived UI decisions (e.g. school module).</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>Role within the family (adult/child/other).</summary>
    public required MemberType MemberType { get; set; }

    /// <summary>
    /// Relative path (inside the files volume) to this member's avatar. Null for default.
    /// </summary>
    public string? PhotoPath { get; set; }

    /// <summary>
    /// Hex color used consistently across calendar, dashboard and avatars for this member.
    /// Format: "#RRGGBB". Enforced by validation at the application layer.
    /// </summary>
    public required string ColorHex { get; set; }

    /// <summary>
    /// Informational contact email (for reminders or sharing with the pediatrician).
    /// Distinct from <see cref="Identity.User.Email"/> (OIDC identity).
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>Linked user account id. Null when this member does not authenticate.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Navigation to the linked <see cref="User"/>.</summary>
    public User? User { get; set; }

    /// <summary>
    /// Soft-delete/archival flag. Inactive members are preserved for history but hidden
    /// from standard selectors.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
