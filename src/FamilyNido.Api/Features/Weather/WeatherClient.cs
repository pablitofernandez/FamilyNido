using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FamilyNido.Api.Features.Weather;

/// <summary>
/// Thin client over the public Open-Meteo forecast endpoint. The API needs no
/// authentication and is free for self-hosted use, so we only forward the
/// coordinates and the family's IANA timezone (so sunrise/sunset land in the
/// local clock rather than UTC).
/// </summary>
public sealed class WeatherClient
{
    private const string Endpoint = "https://api.open-meteo.com/v1/forecast";

    private readonly HttpClient _http;
    private readonly ILogger<WeatherClient> _logger;

    /// <summary>Primary constructor.</summary>
    public WeatherClient(HttpClient http, ILogger<WeatherClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Fetch a today-only forecast for <paramref name="latitude"/>/<paramref name="longitude"/>.
    /// Returns null on transport or upstream errors so callers can degrade
    /// gracefully — there's no point bubbling up a 5xx for a decorative widget.
    /// </summary>
    /// <param name="latitude">Decimal degrees in [-90, 90].</param>
    /// <param name="longitude">Decimal degrees in [-180, 180].</param>
    /// <param name="ianaTimeZone">IANA timezone passed to Open-Meteo so sunrise/sunset come in local time.</param>
    /// <param name="cancellationToken">Cancellation token from the request scope.</param>
    public async Task<OpenMeteoResponse?> GetForecastAsync(
        double latitude,
        double longitude,
        string ianaTimeZone,
        CancellationToken cancellationToken)
    {
        // Compose the URL inline — Open-Meteo expects flat query strings, not
        // a structured body, and using HttpClient with QueryHelpers would only
        // add ceremony for what is essentially a four-parameter GET.
        var url = $"{Endpoint}?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            + $"&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            + $"&current=temperature_2m,apparent_temperature,weather_code"
            + $"&daily=temperature_2m_max,temperature_2m_min,weather_code,sunrise,sunset,precipitation_probability_max"
            + $"&forecast_days=1"
            + $"&timezone={Uri.EscapeDataString(ianaTimeZone)}";

        try
        {
            return await _http.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Open-Meteo request failed for {Lat},{Lon}", latitude, longitude);
            return null;
        }
    }

    /// <summary>Subset of the Open-Meteo response surface used by the widget.</summary>
    /// <param name="Current">Latest "current weather" snapshot.</param>
    /// <param name="Daily">One-day arrays (each list has length 1 because of <c>forecast_days=1</c>).</param>
    public sealed record OpenMeteoResponse(
        [property: JsonPropertyName("current")] CurrentBlock? Current,
        [property: JsonPropertyName("daily")] DailyBlock? Daily);

    /// <summary>"current" block — single instantaneous reading.</summary>
    public sealed record CurrentBlock(
        [property: JsonPropertyName("temperature_2m")] double? Temperature,
        [property: JsonPropertyName("apparent_temperature")] double? ApparentTemperature,
        [property: JsonPropertyName("weather_code")] int? WeatherCode);

    /// <summary>"daily" block — arrays indexed by day.</summary>
    public sealed record DailyBlock(
        [property: JsonPropertyName("temperature_2m_max")] List<double>? Max,
        [property: JsonPropertyName("temperature_2m_min")] List<double>? Min,
        [property: JsonPropertyName("weather_code")] List<int>? WeatherCode,
        [property: JsonPropertyName("sunrise")] List<string>? Sunrise,
        [property: JsonPropertyName("sunset")] List<string>? Sunset,
        [property: JsonPropertyName("precipitation_probability_max")] List<int?>? PrecipitationProbabilityMax);
}
