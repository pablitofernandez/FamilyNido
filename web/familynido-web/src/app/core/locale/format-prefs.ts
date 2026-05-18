import { WeatherToday } from '../models/weather';

/**
 * Tiny helpers that translate the SPA's active locale (Angular's `LOCALE_ID`,
 * one of the i18n bundles we ship — `es-ES` or `en-US` today) into the
 * format conventions an English-speaking US user expects: Fahrenheit
 * temperatures, 12H sunrise/sunset, etc. Everything else stays metric.
 *
 * Bundle-driven by design — keeps the implementation tiny while serving
 * issue #12. If a family ever wants language-and-unit independent settings
 * (e.g. Spanish UI but Fahrenheit), we'd promote this to a Family.* field
 * and surface it on /account.
 */

export type TemperatureUnit = 'C' | 'F';

/**
 * Pick the temperature unit for the active locale. Only `en-US` flips to
 * Fahrenheit; every other locale we ship — and any fallback — stays on
 * Celsius. We could match more imperial locales (en-LR, en-BS, …) later
 * but they're not in our i18n bundles, so adding the check is moot.
 */
export function temperatureUnit(locale: string): TemperatureUnit {
  return locale === 'en-US' ? 'F' : 'C';
}

/** Convert Celsius (what Open-Meteo gives us) to Fahrenheit. */
export function toFahrenheit(celsius: number): number {
  return celsius * 9 / 5 + 32;
}

/** Project a Celsius value into the user's preferred unit. */
export function projectTemperature(celsius: number, unit: TemperatureUnit): number {
  return unit === 'F' ? toFahrenheit(celsius) : celsius;
}

/**
 * Project a whole weather payload into the user's preferred unit and
 * reformat the backend's plain "HH:mm" sunrise/sunset strings through a
 * locale-aware formatter so they honour the 12H/24H convention of the
 * active locale. Returns a fresh object so signals downstream don't share
 * mutable references with the raw API response.
 *
 * `formatTime` is supplied by the caller (each component already builds its
 * own `Intl.DateTimeFormat` instance with the right locale and dimensions —
 * we don't want to construct one per call here).
 */
export function projectWeather(
  weather: WeatherToday,
  unit: TemperatureUnit,
  formatTime: (hhmm: string) => string,
): WeatherToday {
  return {
    ...weather,
    currentTemperature: projectTemperature(weather.currentTemperature, unit),
    apparentTemperature: weather.apparentTemperature !== null
      ? projectTemperature(weather.apparentTemperature, unit)
      : null,
    maxTemperature: projectTemperature(weather.maxTemperature, unit),
    minTemperature: projectTemperature(weather.minTemperature, unit),
    sunrise: weather.sunrise ? formatTime(weather.sunrise) : weather.sunrise,
    sunset: weather.sunset ? formatTime(weather.sunset) : weather.sunset,
  };
}

/**
 * Reparse a backend-provided "HH:mm" string into a string formatted by the
 * supplied `Intl.DateTimeFormat`. Returns the input unchanged when it can't
 * be parsed — keeps the call safe for legacy or malformed inputs.
 */
export function reformatHourMinute(hhmm: string, formatter: Intl.DateTimeFormat): string {
  const match = /^(\d{1,2}):(\d{2})/.exec(hhmm);
  if (!match) return hhmm;
  const d = new Date();
  d.setHours(Number(match[1]), Number(match[2]), 0, 0);
  return formatter.format(d);
}
