import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  DuplicatePreviousWeekRequest,
  MealCourse,
  MealPlanSlotEntry,
  MealSlot,
  MealWeek,
  UpsertMealSlotRequest,
} from '../models/meal';

/** HTTP client for `/api/meals`. */
@Injectable({ providedIn: 'root' })
export class MealsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/meals';

  /** GET /api/meals/week?startDate=YYYY-MM-DD */
  week(startDate: string): Observable<MealWeek> {
    const params = new HttpParams().set('startDate', startDate);
    return this.http.get<MealWeek>(`${this.baseUrl}/week`, { params });
  }

  /** PUT /api/meals/slots */
  upsert(request: UpsertMealSlotRequest): Observable<MealPlanSlotEntry> {
    return this.http.put<MealPlanSlotEntry>(`${this.baseUrl}/slots`, request);
  }

  /** DELETE /api/meals/slots?date=YYYY-MM-DD&slot=Lunch&course=First */
  clear(date: string, slot: MealSlot, course: MealCourse): Observable<void> {
    const params = new HttpParams()
      .set('date', date)
      .set('slot', slot)
      .set('course', course);
    return this.http.delete<void>(`${this.baseUrl}/slots`, { params });
  }

  /** GET /api/meals/suggestions?prefix=ca&limit=8 */
  suggestions(prefix: string, limit?: number): Observable<string[]> {
    let params = new HttpParams().set('prefix', prefix);
    if (limit !== undefined) {
      params = params.set('limit', String(limit));
    }
    return this.http.get<string[]>(`${this.baseUrl}/suggestions`, { params });
  }

  /** POST /api/meals/week/duplicate-previous */
  duplicatePrevious(request: DuplicatePreviousWeekRequest): Observable<MealWeek> {
    return this.http.post<MealWeek>(`${this.baseUrl}/week/duplicate-previous`, request);
  }
}
