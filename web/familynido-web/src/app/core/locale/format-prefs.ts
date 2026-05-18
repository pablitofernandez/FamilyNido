import { TemperatureUnitPreference, TimeFormatPreference } from '../models/me';
import { WeatherToday } from '../models/weather';

/**
 * Helpers that decide how times and temperatures should render for the
 * current viewer. Two layers, in priority order:
 *
 *  1. **Explicit user override** — `User.TimeFormat` / `User.TemperatureUnit`
 *     stored on the backend. The user picked it on /account; honour it
 *     wherever they view FamilyNido.
 *  2. **Bundle-derived auto** — when no override is set, infer from the
 *     active Angular i18n bundle (`LOCALE_ID`). `en-US` → 12H + Fahrenheit;
 *     everything else → 24H + Celsius. Lets fresh installs land on sensible
 *     defaults without forcing the user through a settings page.
 *
 * Keeps the surface tiny on purpose. If we ever support more locales or
 * imperial systems beyond the US, expand the `is*Locale` helpers.
 */

export type TemperatureUnit = 'C' | 'F';

/** Auto-pick the temperature unit for the active locale (used only as fallback). */
export function temperatureUnitFromLocale(locale: string): TemperatureUnit {
  return locale === 'en-US' ? 'F' : 'C';
}

/**
 * Resolve the temperature unit to render for this user. Explicit override
 * wins; falls back to locale inference when the user hasn't picked.
 */
export function resolveTemperatureUnit(
  override: TemperatureUnitPreference | null | undefined,
  locale: string,
): TemperatureUnit {
  if (override === 'Celsius') return 'C';
  if (override === 'Fahrenheit') return 'F';
  return temperatureUnitFromLocale(locale);
}

/**
 * Hour cycle to feed `Intl.DateTimeFormat` when an explicit override is set.
 * For "auto" we return undefined so the formatter picks the locale's native
 * cycle (en-US → h12, es-ES → h23) on its own.
 */
export function resolveHourCycle(
  override: TimeFormatPreference | null | undefined,
): Intl.DateTimeFormatOptions['hourCycle'] | undefined {
  if (override === 'H12') return 'h12';
  if (override === 'H24') return 'h23';
  return undefined;
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
 * reformat the backend's "HH:mm" sunrise/sunset strings through a
 * locale-aware formatter so they honour the chosen hour cycle.
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
 * Reparse a backend "HH:mm" string through the supplied formatter. Returns
 * the input unchanged when it doesn't match — keeps the call safe for
 * legacy or malformed inputs.
 */
export function reformatHourMinute(hhmm: string, formatter: Intl.DateTimeFormat): string {
  const match = /^(\d{1,2}):(\d{2})/.exec(hhmm);
  if (!match) return hhmm;
  const d = new Date();
  d.setHours(Number(match[1]), Number(match[2]), 0, 0);
  return formatter.format(d);
}
