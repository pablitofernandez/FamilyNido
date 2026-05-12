import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { NotificationPreferences } from '../models/notification-preferences';

/** Thin client for `/api/notifications/preferences`. */
@Injectable({ providedIn: 'root' })
export class NotificationPreferencesService {
  private readonly http = inject(HttpClient);

  /** Fetch the caller's preferences (server fills in defaults when no row exists). */
  get(): Observable<NotificationPreferences> {
    return this.http.get<NotificationPreferences>('/api/notifications/preferences');
  }

  /** Replace the caller's preferences in full. */
  update(preferences: NotificationPreferences): Observable<NotificationPreferences> {
    return this.http.put<NotificationPreferences>('/api/notifications/preferences', preferences);
  }

  /**
   * Send the morning digest to the calling user *only* (not the whole family).
   * Useful for previewing the template without waiting for the scheduler.
   * Returns `{ email, isEmpty }` — when the digest had nothing to report,
   * `isEmpty` is true and no email was queued.
   */
  sendMyDigest(): Observable<{ email: string; isEmpty: boolean }> {
    return this.http.post<{ email: string; isEmpty: boolean }>(
      '/api/notifications/digest/me',
      {},
    );
  }
}
