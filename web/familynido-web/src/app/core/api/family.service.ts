import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Family, UpdateFamilyLocationRequest } from '../models/family';

/** Thin client for `/api/family` (read + admin location update). */
@Injectable({ providedIn: 'root' })
export class FamilyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/family';

  /** Read the family profile of the caller (returns 403 when not linked). */
  get(): Observable<Family> {
    return this.http.get<Family>(`${this.baseUrl}/`);
  }

  /** Admin-only — set or clear the family geographic location. */
  updateLocation(request: UpdateFamilyLocationRequest): Observable<Family> {
    return this.http.put<Family>(`${this.baseUrl}/location`, request);
  }
}
