using FamilyNido.Api.Features.Notifications;

namespace FamilyNido.Api.Features.Weather;

/// <summary>
/// Maps WMO weather codes (the integer Open-Meteo returns) to a localised
/// short label and a single emoji. The full WMO code book has dozens of
/// shades; we collapse them into the bands a family glances at on a phone.
/// </summary>
public static class WeatherCodes
{
    /// <summary>Resolve <paramref name="code"/> to a (label, emoji) tuple in the caller's language.</summary>
    /// <param name="code">WMO weather code from Open-Meteo.</param>
    /// <param name="lang">BCP-47 tag of the recipient (typically <see cref="Domain.Identity.User.PreferredLanguage"/>).</param>
    public static (string Label, string Emoji) Describe(int code, string lang)
    {
        var (key, emoji) = code switch
        {
            0 => ("weather.code.0", "☀️"),
            1 => ("weather.code.1", "🌤️"),
            2 => ("weather.code.2", "⛅"),
            3 => ("weather.code.3", "☁️"),
            45 or 48 => ("weather.code.fog", "🌫️"),
            51 or 53 or 55 => ("weather.code.drizzle", "🌦️"),
            56 or 57 => ("weather.code.freezing-drizzle", "🌧️"),
            61 or 63 or 65 => ("weather.code.rain", "🌧️"),
            66 or 67 => ("weather.code.freezing-rain", "🌧️"),
            71 or 73 or 75 => ("weather.code.snow", "🌨️"),
            77 => ("weather.code.sleet", "🌨️"),
            80 or 81 or 82 => ("weather.code.showers", "🌧️"),
            85 or 86 => ("weather.code.snow-showers", "🌨️"),
            95 => ("weather.code.thunderstorm", "⛈️"),
            96 or 99 => ("weather.code.thunderstorm-hail", "⛈️"),
            _ => ("weather.code.unknown", "🌡️"),
        };
        return (BackendLocalization.T(key, lang), emoji);
    }
}
