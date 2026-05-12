import { FamilyRole } from './family-member';

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
}
