using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using Microsoft.Extensions.Caching.Memory;

namespace FamilyNido.Api.Features.Weather;

/// <summary>
/// Slice for <c>GET /api/weather/today</c>. Resolves the family location,
/// hits Open-Meteo through <see cref="WeatherClient"/>, and projects the
/// upstream response into the compact widget DTO. Responses are cached for
/// 30 minutes per <c>(latitude, longitude)</c> so a busy household reloading
/// the dashboard never melts the upstream service.
/// </summary>
public static class GetWeatherToday
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    /// <summary>Query carries no payload — the family's location is resolved server-side.</summary>
    public sealed record Query : IRequest<Result<WeatherTodayDto>>;

    /// <summary>Handler with in-memory cache + graceful degradation when upstream fails.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<WeatherTodayDto>>
    {
        private readonly ICurrentUserContext _userContext;
        private readonly WeatherClient _client;
        private readonly IMemoryCache _cache;

        /// <summary>Primary constructor.</summary>
        public Handler(ICurrentUserContext userContext, WeatherClient client, IMemoryCache cache)
        {
            _userContext = userContext;
            _client = client;
            _cache = cache;
        }

        /// <inheritdoc />
        public async Task<Result<WeatherTodayDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family.");
            }

            var family = current.Family;
            if (family.Latitude is not { } lat || family.Longitude is not { } lon)
            {
                return ApplicationError.NotFound("weather.no_location", "Family location is not configured.");
            }

            // Cache key keeps coordinates and the timezone — sunrise/sunset depend
            // on the timezone, so a TZ change must invalidate the cached value.
            // The label is NOT keyed by language: we cache the WMO code and
            // re-resolve the localized label per request, so two members of
            // the same family with different PreferredLanguage hit one entry
            // each and read it in their own tongue.
            var lang = current.User.PreferredLanguage;
            var cacheKey = $"weather:{lat:F3},{lon:F3}:{family.TimeZone}";
            if (_cache.TryGetValue<WeatherTodayDto>(cacheKey, out var cached) && cached is not null)
            {
                return WithLocale(cached, family.LocationLabel ?? string.Empty, lang);
            }

            var response = await _client.GetForecastAsync(lat, lon, family.TimeZone, cancellationToken);
            if (response?.Current is null || response.Daily is null
                || response.Daily.Max is null || response.Daily.Max.Count == 0
                || response.Daily.Min is null || response.Daily.Min.Count == 0)
            {
                return ApplicationError.NotFound("weather.upstream_failed", "Could not reach the weather provider.");
            }

            var code = response.Current.WeatherCode
                ?? (response.Daily.WeatherCode is { Count: > 0 } w ? w[0] : 0);
            var (label, emoji) = WeatherCodes.Describe(code, lang);

            var dto = new WeatherTodayDto(
                LocationLabel: family.LocationLabel ?? string.Empty,
                CurrentTemperature: Math.Round(response.Current.Temperature ?? response.Daily.Max[0], 1),
                ApparentTemperature: response.Current.ApparentTemperature is { } apt ? Math.Round(apt, 1) : null,
                MaxTemperature: Math.Round(response.Daily.Max[0], 1),
                MinTemperature: Math.Round(response.Daily.Min[0], 1),
                WeatherCode: code,
                WeatherLabel: label,
                WeatherIcon: emoji,
                PrecipitationProbability: response.Daily.PrecipitationProbabilityMax is { Count: > 0 } pp ? pp[0] : null,
                Sunrise: response.Daily.Sunrise is { Count: > 0 } sr ? FormatLocalTime(sr[0]) : null,
                Sunset: response.Daily.Sunset is { Count: > 0 } ss ? FormatLocalTime(ss[0]) : null);

            _cache.Set(cacheKey, dto, CacheTtl);
            return dto;
        }

        /// <summary>Open-Meteo returns sunrise/sunset as <c>"yyyy-MM-ddTHH:mm"</c> in the requested timezone.</summary>
        private static string FormatLocalTime(string raw)
        {
            // We only need the HH:mm slice — the date part is always today
            // because we asked for forecast_days=1.
            var t = raw.LastIndexOf('T');
            if (t < 0 || t + 6 > raw.Length) return raw;
            return raw.Substring(t + 1, 5);
        }

        /// <summary>
        /// Patch the cached payload with the (mutable) family label and the
        /// caller's localized weather label / icon. The cache stores the WMO
        /// code as the source of truth, so the description is always derived
        /// fresh from <see cref="WeatherCodes.Describe(int, string)"/>.
        /// </summary>
        private static WeatherTodayDto WithLocale(WeatherTodayDto cached, string locationLabel, string lang)
        {
            var (label, emoji) = WeatherCodes.Describe(cached.WeatherCode, lang);
            return cached with
            {
                LocationLabel = locationLabel,
                WeatherLabel = label,
                WeatherIcon = emoji,
            };
        }
    }
}
