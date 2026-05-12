import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  MemberAgendaException,
  MemberAgendaExceptionInput,
  MemberAgendaOverview,
  MemberAgendaPattern,
  MemberAgendaPatternInput,
} from '../models/agenda';

/** Thin client for `/api/member-agenda/*`. */
@Injectable({ providedIn: 'root' })
export class MemberAgendaService {
  private readonly http = inject(HttpClient);

  /** Fetch the family-wide overview for the inclusive [from, to] range. */
  overview(from: string, to: string): Observable<MemberAgendaOverview> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<MemberAgendaOverview>('/api/member-agenda/overview', { params });
  }

  createPattern(body: MemberAgendaPatternInput): Observable<MemberAgendaPattern> {
    return this.http.post<MemberAgendaPattern>('/api/member-agenda/patterns', body);
  }

  updatePattern(id: string, body: MemberAgendaPatternInput): Observable<MemberAgendaPattern> {
    return this.http.put<MemberAgendaPattern>(`/api/member-agenda/patterns/${id}`, body);
  }

  deletePattern(id: string): Observable<void> {
    return this.http.delete<void>(`/api/member-agenda/patterns/${id}`);
  }

  createException(body: MemberAgendaExceptionInput): Observable<MemberAgendaException> {
    return this.http.post<MemberAgendaException>('/api/member-agenda/exceptions', body);
  }

  updateException(id: string, body: MemberAgendaExceptionInput): Observable<MemberAgendaException> {
    return this.http.put<MemberAgendaException>(`/api/member-agenda/exceptions/${id}`, body);
  }

  deleteException(id: string): Observable<void> {
    return this.http.delete<void>(`/api/member-agenda/exceptions/${id}`);
  }
}
