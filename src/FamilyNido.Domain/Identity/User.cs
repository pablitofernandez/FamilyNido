using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Identity;

/// <summary>
/// Authenticable account in FamilyNido. Identity is provider-agnostic: a user
/// proves who they are via one or more <see cref="UserCredential"/> rows
/// (OIDC subject, local password hash, …). The internal id never changes
/// across credential rotations.
/// </summary>
/// <remarks>
/// Linking a user to a <see cref="FamilyMember"/> is an explicit administrative
/// action (RF-AUTH-003): orphan users (no member) are rejected at the API
/// surface until an admin links them, typically by accepting an
/// <see cref="Invitation"/>.
/// </remarks>
public sealed class User : AuditableEntity
{
    /// <summary>Email claim at the time of login. Used as the lookup key for local login.</summary>
    public required string Email { get; set; }

    /// <summary>Display name claim at the time of login (fallback to the email local-part).</summary>
    public required string DisplayName { get; set; }

    /// <summary>Authorization role used by ASP.NET Core policies.</summary>
    public FamilyRole Role { get; set; } = FamilyRole.Adult;

    /// <summary>Timestamp of the most recent successful login (any credential).</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Watermark used by the wall unread-counter (RF-WALL-010). Messages created after
    /// this instant count as unread for this user until they open the wall again.
    /// </summary>
    public DateTimeOffset? LastWallReadAt { get; set; }

    /// <summary>
    /// IETF BCP-47 tag picked by the user for the FamilyNido UI. Used server-side
    /// to render digest emails, mention notifications, and integration-generated
    /// task titles in the right language. Defaults to <c>es-ES</c> — the
    /// product's source locale.
    /// </summary>
    public string PreferredLanguage { get; set; } = "es-ES";

    /// <summary>
    /// Optional explicit override for the time format the SPA renders for
    /// this user. <c>null</c> means "auto" — let the frontend infer from the
    /// active i18n bundle (en-US → 12H, es-ES → 24H).
    /// </summary>
    public TimeFormatPreference? TimeFormat { get; set; }

    /// <summary>
    /// Optional explicit override for the temperature unit shown to this user
    /// in the weather widget. <c>null</c> means "auto" — derived from the
    /// active i18n bundle (en-US → Fahrenheit, anything else → Celsius).
    /// </summary>
    public TemperatureUnitPreference? TemperatureUnit { get; set; }

    /// <summary>Navigation back to the linked family member, if any.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Authentication credentials owned by this user (1..N).</summary>
    public ICollection<UserCredential> Credentials { get; set; } = new List<UserCredential>();
}
