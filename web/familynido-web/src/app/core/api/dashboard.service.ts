import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { DashboardPreferences } from '../models/dashboard';

/** Thin client for `/api/dashboard/preferences`. */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  /** Fetch the caller's reconciled widget layout (server appends new widgets). */
  getPreferences(): Observable<DashboardPreferences> {
    return this.http.get<DashboardPreferences>('/api/dashboard/preferences');
  }

  /** Replace the caller's widget layout with the supplied ordered list. */
  updatePreferences(preferences: DashboardPreferences): Observable<DashboardPreferences> {
    return this.http.put<DashboardPreferences>('/api/dashboard/preferences', preferences);
  }
}
