/** TypeScript counterparts of the calendar DTOs returned by /api/calendar. */

/** A Google account linked under the family — one per (user, Google email). */
export interface GoogleAccount {
  id: string;
  email: string;
  displayName: string | null;
  isRevoked: boolean;
  lastError: string | null;
  /** UTC instant the account was linked. */
  linkedAt: string;
  calendars: LinkedCalendar[];
}

/** A specific Google calendar discovered under a linked account. */
export interface LinkedCalendar {
  id: string;
  externalCalendarId: string;
  summary: string;
  description: string | null;
  /** Hex color (#RRGGBB) reported by Google, used as fallback when no member assigned. */
  colorHex: string | null;
  /** Whether events from this calendar are mirrored into FamilyNido. */
  isImported: boolean;
  /** Optional family member id — drives event color in the UI. */
  familyMemberId: string | null;
  /** UTC instant of the last successful sync. Null until first sync. */
  lastSyncedAt: string | null;
}

/** Mirrored Google Calendar event (read-only against Google; relatedMemberIds is local). */
export interface CalendarEvent {
  id: string;
  linkedCalendarId: string;
  /** Family member of the source calendar, if assigned. */
  familyMemberId: string | null;
  title: string;
  description: string | null;
  location: string | null;
  /** Start instant (UTC). */
  startAt: string;
  /** End instant (UTC). */
  endAt: string;
  isAllDay: boolean;
  /**
   * For all-day events, the inclusive start date (YYYY-MM-DD) as it appears
   * in Google Calendar. Interpreted server-side in `originalTimeZone` so the
   * value does NOT need any further conversion in the browser. Null for
   * timed events — use `startAt` instead.
   */
  startDate: string | null;
  /** Exclusive end date (YYYY-MM-DD) for all-day events; null for timed events. */
  endDate: string | null;
  originalTimeZone: string | null;
  /** Public link back to the event in Google Calendar. */
  htmlLink: string | null;
  /** Per-event N:M of family members tagged locally (independent of the calendar default). */
  relatedMemberIds: string[];
}

/** Body for PATCH /api/calendar/calendars/{id}. */
export interface UpdateLinkedCalendarRequest {
  isImported: boolean;
  familyMemberId: string | null;
}

/** Response of POST /api/calendar/google/start. */
export interface StartGoogleLinkResponse {
  authUrl: string;
}
