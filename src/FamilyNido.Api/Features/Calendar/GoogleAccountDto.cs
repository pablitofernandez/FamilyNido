using FamilyNido.Domain.Calendar;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>Wire-level view of a linked Google account plus its discovered calendars.</summary>
/// <param name="Id">FamilyNido id of the account row.</param>
/// <param name="Email">Google email — labels the account in the UI.</param>
/// <param name="DisplayName">Best-effort display name reported by Google.</param>
/// <param name="IsRevoked">True when the refresh token was rejected; the user must re-link.</param>
/// <param name="LastError">Last sync error message captured for diagnostics; null when healthy.</param>
/// <param name="LinkedAt">UTC instant the account was linked.</param>
/// <param name="Calendars">Calendars discovered under this account.</param>
public sealed record GoogleAccountDto(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsRevoked,
    string? LastError,
    DateTimeOffset LinkedAt,
    IReadOnlyList<LinkedCalendarDto> Calendars)
{
    /// <summary>Builds a DTO from the persisted entity (with eager-loaded calendars).</summary>
    public static GoogleAccountDto From(GoogleAccount account)
        => new(
            account.Id,
            account.Email,
            account.DisplayName,
            account.IsRevoked,
            account.LastError,
            account.CreatedAt,
            [.. account.Calendars
                .OrderBy(c => c.Summary, StringComparer.OrdinalIgnoreCase)
                .Select(LinkedCalendarDto.From)]);
}

/// <summary>Wire-level view of a single Google calendar discovered under an account.</summary>
/// <param name="Id">FamilyNido id of the linked-calendar row.</param>
/// <param name="ExternalCalendarId">Google's calendar id (stable across accounts).</param>
/// <param name="Summary">Display name from Google.</param>
/// <param name="Description">Optional description from Google.</param>
/// <param name="ColorHex">Color reported by Google (<c>#RRGGBB</c>).</param>
/// <param name="IsImported">Whether events from this calendar are mirrored.</param>
/// <param name="FamilyMemberId">Optional family member assigned for color attribution.</param>
/// <param name="LastSyncedAt">UTC instant of the last successful sync; null until first sync.</param>
public sealed record LinkedCalendarDto(
    Guid Id,
    string ExternalCalendarId,
    string Summary,
    string? Description,
    string? ColorHex,
    bool IsImported,
    Guid? FamilyMemberId,
    DateTimeOffset? LastSyncedAt)
{
    /// <summary>Builds a DTO from the persisted entity.</summary>
    public static LinkedCalendarDto From(LinkedCalendar calendar)
        => new(
            calendar.Id,
            calendar.ExternalCalendarId,
            calendar.Summary,
            calendar.Description,
            calendar.ColorHex,
            calendar.IsImported,
            calendar.FamilyMemberId,
            calendar.LastSyncedAt);
}
