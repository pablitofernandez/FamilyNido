/** How the kid commutes to / from school. Drives slot semantics + dashboard icons. */
export type TransportMode = 'None' | 'Bus' | 'Walk' | 'Car';

/** Static school card for one member. */
export interface SchoolProfile {
  schoolName: string | null;
  grade: string | null;
  tutor: string | null;
  transportMode: TransportMode;
  morningTime: string | null;   // HH:mm:ss
  afternoonTime: string | null; // HH:mm:ss
  notes: string | null;
}

/**
 * Single weekly slot of the school-day schedule. The API ships
 * <c>DayOfWeek</c> as its .NET name through <c>JsonStringEnumConverter</c>
 * (so it arrives as "Monday", "Tuesday"… on reads). On writes the server
 * accepts both the name and the numeric ordinal, so callers are free to
 * send either — within the component we normalise to numbers.
 */
export interface SchoolDayScheduleSlot {
  dayOfWeek: number | string;
  dropoffMemberId: string | null;
  pickupMemberId: string | null;
}

/** Per-date override of the school-day schedule. */
export interface SchoolDayException {
  id: string;
  memberId: string;
  date: string; // YYYY-MM-DD
  isCancelled: boolean;
  dropoffMemberId: string | null;
  pickupMemberId: string | null;
  /** Override entry time for this date, null when unchanged. */
  morningTime: string | null;
  /** Override exit time for this date, null when unchanged. */
  afternoonTime: string | null;
  notes: string | null;
}

/** Family-wide school holiday. */
export interface SchoolHoliday {
  id: string;
  startDate: string;
  endDate: string;
  label: string;
}

/** Resolved school day for a (kid, date) cell shown in the weekly grid. */
export interface ResolvedSchoolDay {
  memberId: string;
  date: string;
  /** Kid's transport mode — drives the dashboard / widget icon. */
  transportMode: TransportMode;
  dropoffMemberId: string | null;
  pickupMemberId: string | null;
  /** Effective entry time (exception override → profile default → null). */
  morningTime: string | null;
  /** Effective exit time (exception override → profile default → null). */
  afternoonTime: string | null;
  isCancelled: boolean;
  holidayLabel: string | null;
  notes: string | null;
}

/** Compact view of the weekly schedule grouped by kid. */
export interface KidSchedule {
  memberId: string;
  slots: SchoolDayScheduleSlot[];
}

/** Wire shape of an after-school activity. */
export interface Extracurricular {
  id: string;
  memberId: string;
  name: string;
  location: string | null;
  contactPhone: string | null;
  weeklyDays: string;
  startTime: string;
  endTime: string;
  startDate: string;
  endDate: string | null;
  defaultDropoffMemberId: string | null;
  defaultPickupMemberId: string | null;
  notes: string | null;
  isArchived: boolean;
}

export interface ExtracurricularException {
  id: string;
  extracurricularId: string;
  date: string;
  isCancelled: boolean;
  dropoffMemberId: string | null;
  pickupMemberId: string | null;
  notes: string | null;
}

/** Resolved extracurricular session for a given day. */
export interface ResolvedExtracurricular {
  extracurricularId: string;
  memberId: string;
  date: string;
  startTime: string;
  endTime: string;
  name: string;
  location: string | null;
  contactPhone: string | null;
  dropoffMemberId: string | null;
  pickupMemberId: string | null;
  isCancelled: boolean;
  holidayLabel: string | null;
  notes: string | null;
}

/** Aggregate returned by GET /api/school/overview. */
export interface SchoolOverview {
  from: string;
  to: string;
  schedule: KidSchedule[];
  dayExceptions: SchoolDayException[];
  holidays: SchoolHoliday[];
  resolvedDays: ResolvedSchoolDay[];
  extracurriculars: Extracurricular[];
  extracurricularExceptions: ExtracurricularException[];
  resolvedExtracurriculars: ResolvedExtracurricular[];
}

export interface UpsertSchoolProfileRequest {
  schoolName: string | null;
  grade: string | null;
  tutor: string | null;
  transportMode: TransportMode;
  morningTime: string | null;
  afternoonTime: string | null;
  notes: string | null;
}

export interface ReplaceScheduleRequest {
  slots: SchoolDayScheduleSlot[];
}

export interface DayExceptionRequest {
  isCancelled: boolean;
  dropoffMemberId: string | null;
  pickupMemberId: string | null;
  /** Override entry time (HH:mm:ss). Null = no time override for this day. */
  morningTime: string | null;
  /** Override exit time (HH:mm:ss). Null = no time override for this day. */
  afternoonTime: string | null;
  notes: string | null;
}

export interface HolidayRequest {
  startDate: string;
  endDate: string;
  label: string;
}

export interface ExtracurricularRequest {
  memberId: string;
  name: string;
  location: string | null;
  contactPhone: string | null;
  weeklyDays: string;
  startTime: string;
  endTime: string;
  startDate: string;
  endDate: string | null;
  defaultDropoffMemberId: string | null;
  defaultPickupMemberId: string | null;
  notes: string | null;
}

export interface ExtracurricularExceptionRequest {
  isCancelled: boolean;
  dropoffMemberId: string | null;
  pickupMemberId: string | null;
  notes: string | null;
}
