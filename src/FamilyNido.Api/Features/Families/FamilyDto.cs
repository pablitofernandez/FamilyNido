namespace FamilyNido.Api.Features.Families;

/// <summary>Wire shape returned by the family settings endpoint.</summary>
/// <param name="Id">Family id.</param>
/// <param name="Name">Display name shown in the header.</param>
/// <param name="TimeZone">IANA timezone (e.g. "Europe/Madrid").</param>
/// <param name="Locale">BCP-47 locale used for default formatting.</param>
/// <param name="Latitude">Geographic latitude in decimal degrees, or null when not configured.</param>
/// <param name="Longitude">Geographic longitude paired with latitude.</param>
/// <param name="LocationLabel">Human-readable location label (city/town/village).</param>
public sealed record FamilyDto(
    Guid Id,
    string Name,
    string TimeZone,
    string Locale,
    double? Latitude,
    double? Longitude,
    string? LocationLabel);
