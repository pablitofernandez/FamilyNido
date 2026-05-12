import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  DayExceptionRequest,
  Extracurricular,
  ExtracurricularException,
  ExtracurricularExceptionRequest,
  ExtracurricularRequest,
  HolidayRequest,
  KidSchedule,
  ReplaceScheduleRequest,
  SchoolDayException,
  SchoolHoliday,
  SchoolOverview,
  SchoolProfile,
  UpsertSchoolProfileRequest,
} from '../models/school';

/** Thin client for `/api/school/*`. */
@Injectable({ providedIn: 'root' })
export class SchoolService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/school';

  /** GET /api/school/overview?from=&to= — full week (or any range) snapshot. */
  overview(from: string, to: string): Observable<SchoolOverview> {
    return this.http.get<SchoolOverview>(`${this.baseUrl}/overview`, {
      params: new HttpParams().set('from', from).set('to', to),
    });
  }

  // ── profile ──────────────────────────────────────────────────────────────

  getProfile(memberId: string): Observable<SchoolProfile | null> {
    return this.http.get<SchoolProfile | null>(`${this.baseUrl}/members/${memberId}/profile`);
  }

  upsertProfile(memberId: string, body: UpsertSchoolProfileRequest): Observable<SchoolProfile> {
    return this.http.put<SchoolProfile>(`${this.baseUrl}/members/${memberId}/profile`, body);
  }

  // ── school-day schedule ──────────────────────────────────────────────────

  replaceDaySchedule(memberId: string, body: ReplaceScheduleRequest): Observable<KidSchedule> {
    return this.http.put<KidSchedule>(`${this.baseUrl}/members/${memberId}/day-schedule`, body);
  }

  setDayException(memberId: string, date: string, body: DayExceptionRequest): Observable<SchoolDayException> {
    return this.http.put<SchoolDayException>(`${this.baseUrl}/day-schedule/exceptions/${memberId}/${date}`, body);
  }

  removeDayException(memberId: string, date: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/day-schedule/exceptions/${memberId}/${date}`);
  }

  // ── holidays ─────────────────────────────────────────────────────────────

  addHoliday(body: HolidayRequest): Observable<SchoolHoliday> {
    return this.http.post<SchoolHoliday>(`${this.baseUrl}/holidays`, body);
  }

  updateHoliday(id: string, body: HolidayRequest): Observable<SchoolHoliday> {
    return this.http.put<SchoolHoliday>(`${this.baseUrl}/holidays/${id}`, body);
  }

  deleteHoliday(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/holidays/${id}`);
  }

  // ── extracurriculars ─────────────────────────────────────────────────────

  listExtracurriculars(includeArchived = false): Observable<Extracurricular[]> {
    let params = new HttpParams();
    if (includeArchived) params = params.set('includeArchived', 'true');
    return this.http.get<Extracurricular[]>(`${this.baseUrl}/extracurriculars`, { params });
  }

  addExtracurricular(body: ExtracurricularRequest): Observable<Extracurricular> {
    return this.http.post<Extracurricular>(`${this.baseUrl}/extracurriculars`, body);
  }

  updateExtracurricular(id: string, body: ExtracurricularRequest): Observable<Extracurricular> {
    return this.http.put<Extracurricular>(`${this.baseUrl}/extracurriculars/${id}`, body);
  }

  archiveExtracurricular(id: string): Observable<Extracurricular> {
    return this.http.patch<Extracurricular>(`${this.baseUrl}/extracurriculars/${id}/archive`, {});
  }

  restoreExtracurricular(id: string): Observable<Extracurricular> {
    return this.http.patch<Extracurricular>(`${this.baseUrl}/extracurriculars/${id}/restore`, {});
  }

  deleteExtracurricular(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/extracurriculars/${id}`);
  }

  setExtracurricularException(id: string, date: string, body: ExtracurricularExceptionRequest): Observable<ExtracurricularException> {
    return this.http.put<ExtracurricularException>(`${this.baseUrl}/extracurriculars/${id}/exceptions/${date}`, body);
  }

  removeExtracurricularException(id: string, date: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/extracurriculars/${id}/exceptions/${date}`);
  }
}
