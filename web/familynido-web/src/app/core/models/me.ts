import { FamilyRole } from './family-member';

/** Explicit time-format override; null = "auto" (let the SPA infer from the bundle). */
export type TimeFormatPreference = 'H12' | 'H24';

/** Explicit temperature-unit override; null = "auto". */
export type TemperatureUnitPreference = 'Celsius' | 'Fahrenheit';

/** Profile payload returned by `GET /api/auth/me`. */
export interface Me {
  userId: string;
  email: string;
  displayName: string;
  role: FamilyRole;
  familyId: string;
  familyName: string;
  memberId: string | null;
  memberDisplayName: string | null;
  colorHex: string | null;
  /** Relative path to the member's avatar image (null when no photo set). */
  photoPath: string | null;
  /** BCP-47 tag the user picked for the UI (e.g. `es-ES`, `en-US`). */
  preferredLanguage: string;
  /** Explicit 12H/24H override, or null to fall back to the active bundle's default. */
  timeFormat: TimeFormatPreference | null;
  /** Explicit Celsius/Fahrenheit override, or null to fall back to the active bundle. */
  temperatureUnit: TemperatureUnitPreference | null;
}
