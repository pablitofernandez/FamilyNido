import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CalendarEvent,
  GoogleAccount,
  LinkedCalendar,
  StartGoogleLinkResponse,
  UpdateLinkedCalendarRequest,
} from '../models/calendar';

/** HTTP client for the /api/calendar endpoints. Thin passthrough. */
@Injectable({ providedIn: 'root' })
export class CalendarService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/calendar';

  /** GET /api/calendar/events?from=&to=&memberIds= */
  listEvents(options: {
    from: Date;
    to: Date;
    memberIds?: string[];
  }): Observable<CalendarEvent[]> {
    let params = new HttpParams()
      .set('from', options.from.toISOString())
      .set('to', options.to.toISOString());
    if (options.memberIds?.length) {
      for (const id of options.memberIds) {
        params = params.append('memberIds', id);
      }
    }
    return this.http.get<CalendarEvent[]>(`${this.baseUrl}/events`, { params });
  }

  /** PUT /api/calendar/events/{eventId}/members — replaces the full member set. */
  setEventMembers(eventId: string, memberIds: string[]): Observable<CalendarEvent> {
    return this.http.put<CalendarEvent>(
      `${this.baseUrl}/events/${encodeURIComponent(eventId)}/members`,
      { memberIds });
  }

  /** GET /api/calendar/accounts */
  listAccounts(): Observable<GoogleAccount[]> {
    return this.http.get<GoogleAccount[]>(`${this.baseUrl}/accounts`);
  }

  /** PATCH /api/calendar/calendars/{id} */
  updateCalendar(id: string, request: UpdateLinkedCalendarRequest): Observable<LinkedCalendar> {
    return this.http.patch<LinkedCalendar>(`${this.baseUrl}/calendars/${id}`, request);
  }

  /** DELETE /api/calendar/accounts/{id} */
  unlinkAccount(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/accounts/${id}`);
  }

  /** POST /api/calendar/accounts/{id}/sync */
  triggerManualSync(id: string): Observable<GoogleAccount> {
    return this.http.post<GoogleAccount>(`${this.baseUrl}/accounts/${id}/sync`, {});
  }

  /**
   * POST /api/calendar/google/start — returns the Google authorization URL the
   * browser must navigate to. The API also writes the OAuth state cookie as part
   * of the response, so a follow-up GET to /api/calendar/google/callback can
   * round-trip it.
   */
  startGoogleLink(): Observable<StartGoogleLinkResponse> {
    return this.http.post<StartGoogleLinkResponse>(`${this.baseUrl}/google/start`, {});
  }
}
