/** Means of transport — mirrors the backend `AgendaTransportMode` enum. */
export type AgendaTransportMode =
  | 'None'
  | 'Car'
  | 'Bus'
  | 'Walk'
  | 'Train'
  | 'Plane'
  | 'Other';

/** ISO weekday name returned by the backend (string-serialised .NET DayOfWeek). */
export type AgendaDayOfWeek =
  | 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday'
  | 'Thursday' | 'Friday' | 'Saturday';

/** Recurring weekly entry for a member. */
export interface MemberAgendaPattern {
  id: string;
  memberId: string;
  dayOfWeek: AgendaDayOfWeek;
  label: string;
  location: string | null;
  /** "HH:mm" or "HH:mm:ss". Null = all-day. */
  startTime: string | null;
  endTime: string | null;
  transportMode: AgendaTransportMode;
  isAway: boolean;
  notes: string | null;
  isActive: boolean;
}

/** Per-date deviation: override of a pattern (patternId set) or ad-hoc (null). */
export interface MemberAgendaException {
  id: string;
  memberId: string;
  /** YYYY-MM-DD */
  date: string;
  patternId: string | null;
  isCancelled: boolean;
  label: string | null;
  location: string | null;
  startTime: string | null;
  endTime: string | null;
  transportMode: AgendaTransportMode | null;
  isAway: boolean | null;
  notes: string | null;
}

/** Resolved agenda entry for a single (member, date) cell — already merged. */
export interface ResolvedAgendaEntry {
  memberId: string;
  date: string;
  patternId: string | null;
  exceptionId: string | null;
  label: string;
  location: string | null;
  startTime: string | null;
  endTime: string | null;
  transportMode: AgendaTransportMode;
  isAway: boolean;
  notes: string | null;
}

/** Bundle returned by the overview endpoint. */
export interface MemberAgendaOverview {
  from: string;
  to: string;
  patterns: MemberAgendaPattern[];
  exceptions: MemberAgendaException[];
  resolved: ResolvedAgendaEntry[];
}

/** Body for create/update pattern. */
export interface MemberAgendaPatternInput {
  memberId: string;
  dayOfWeek: AgendaDayOfWeek;
  label: string;
  location: string | null;
  startTime: string | null;
  endTime: string | null;
  transportMode: AgendaTransportMode;
  isAway: boolean;
  notes: string | null;
  isActive: boolean;
}

/** Body for create/update exception. */
export interface MemberAgendaExceptionInput {
  memberId: string;
  date: string;
  patternId: string | null;
  isCancelled: boolean;
  label: string | null;
  location: string | null;
  startTime: string | null;
  endTime: string | null;
  transportMode: AgendaTransportMode | null;
  isAway: boolean | null;
  notes: string | null;
}
