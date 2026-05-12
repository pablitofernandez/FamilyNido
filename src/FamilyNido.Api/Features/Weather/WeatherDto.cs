namespace FamilyNido.Api.Features.Weather;

/// <summary>
/// Compact "today's weather" payload returned by <c>GET /api/weather/today</c>.
/// Built from a single Open-Meteo forecast call so the frontend stays decoupled
/// from the upstream provider's wire format.
/// </summary>
/// <param name="LocationLabel">Family's location label (e.g. "Bilbao"). Empty when not configured.</param>
/// <param name="CurrentTemperature">Current temperature in °C.</param>
/// <param name="ApparentTemperature">"Feels like" temperature in °C, or null when not available.</param>
/// <param name="MaxTemperature">Today's maximum temperature in °C.</param>
/// <param name="MinTemperature">Today's minimum temperature in °C.</param>
/// <param name="WeatherCode">WMO weather code (0..99). Drives the icon and label.</param>
/// <param name="WeatherLabel">Localised label derived from <see cref="WeatherCode"/>.</param>
/// <param name="WeatherIcon">Single emoji used by the dashboard widget.</param>
/// <param name="PrecipitationProbability">Highest hourly probability of precipitation today (0..100), or null.</param>
/// <param name="Sunrise">Local time of sunrise, or null when not available.</param>
/// <param name="Sunset">Local time of sunset, or null when not available.</param>
public sealed record WeatherTodayDto(
    string LocationLabel,
    double CurrentTemperature,
    double? ApparentTemperature,
    double MaxTemperature,
    double MinTemperature,
    int WeatherCode,
    string WeatherLabel,
    string WeatherIcon,
    int? PrecipitationProbability,
    string? Sunrise,
    string? Sunset);
