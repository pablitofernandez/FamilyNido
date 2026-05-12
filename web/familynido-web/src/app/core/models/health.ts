/** Static "card" of a member's medical baseline. */
export interface HealthProfile {
  bloodType: string | null;
  allergies: string | null;
  chronicConditions: string | null;
  notes: string | null;
}

/** One vaccination row. */
export interface Vaccination {
  id: string;
  name: string;
  date: string; // YYYY-MM-DD
  nextDueDate: string | null;
  notes: string | null;
}

/** One medication row. `isActive` is server-computed (no end date or end date in the future). */
export interface Medication {
  id: string;
  name: string;
  dose: string | null;
  frequency: string | null;
  startDate: string;
  endDate: string | null;
  instructions: string | null;
  isActive: boolean;
}

/** Aggregate returned by `GET /api/health/members/{id}`. */
export interface MemberHealth {
  memberId: string;
  memberDisplayName: string;
  profile: HealthProfile | null;
  vaccinations: Vaccination[];
  medications: Medication[];
}

/** Body for `PUT /api/health/members/{id}/profile`. */
export interface UpsertHealthProfileRequest {
  bloodType: string | null;
  allergies: string | null;
  chronicConditions: string | null;
  notes: string | null;
}

/** Body shared by add/update vaccination. */
export interface VaccinationRequest {
  name: string;
  date: string;
  nextDueDate: string | null;
  notes: string | null;
}

/** Body shared by add/update medication. */
export interface MedicationRequest {
  name: string;
  dose: string | null;
  frequency: string | null;
  startDate: string;
  endDate: string | null;
  instructions: string | null;
}
