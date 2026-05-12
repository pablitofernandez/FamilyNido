using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;

namespace FamilyNido.Domain.Calendar;

/// <summary>
/// Google account linked by an adult member to mirror their Google Calendar
/// events into FamilyNido. Each user may link several accounts (personal + work,
/// shared family account, etc.). The refresh token is the only persisted credential
/// and is stored encrypted with ASP.NET Core Data Protection.
/// </summary>
public sealed class GoogleAccount : AuditableEntity
{
    /// <summary>Family this account belongs to (denormalized for fast filtering).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Owning user — only that user may unlink the account.</summary>
    public required Guid UserId { get; set; }

    /// <summary>Navigation to the owning <see cref="User"/>.</summary>
    public User? User { get; set; }

    /// <summary>Google account email. Surfaced in the UI to disambiguate multiple links.</summary>
    public required string Email { get; set; }

    /// <summary>Display name reported by Google's user info endpoint, best effort.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Refresh token persisted as ciphertext (Data Protection purpose
    /// <c>"calendar.google.refresh-token"</c>). Decrypted only inside the sync
    /// pipeline at request time.
    /// </summary>
    public required string EncryptedRefreshToken { get; set; }

    /// <summary>Last sync error message captured for diagnostics; null when healthy.</summary>
    public string? LastError { get; set; }

    /// <summary>
    /// True when Google rejected our refresh token (revoked by the user, password
    /// change, scope removal). The UI must prompt the user to re-link.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>Calendars discovered for this account; some may be marked as not imported.</summary>
    public ICollection<LinkedCalendar> Calendars { get; set; } = [];
}
