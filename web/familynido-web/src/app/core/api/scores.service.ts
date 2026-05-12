import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Leaderboard, MemberCompletion, MemberScore } from '../models/scores';

/** Thin client for `/api/scores/*`. */
@Injectable({ providedIn: 'root' })
export class ScoresService {
  private readonly http = inject(HttpClient);

  /** Scoreboard for the inclusive [from, to] range. */
  leaderboard(from: string, to: string): Observable<Leaderboard> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<Leaderboard>('/api/scores/leaderboard', { params });
  }

  /** Per-member totals (this week / month / all time). */
  member(memberId: string): Observable<MemberScore> {
    return this.http.get<MemberScore>(`/api/scores/members/${memberId}`);
  }

  /** Latest task completions of a member (default 50, max 200). */
  history(memberId: string, limit?: number): Observable<MemberCompletion[]> {
    let params = new HttpParams();
    if (limit) params = params.set('limit', String(limit));
    return this.http.get<MemberCompletion[]>(
      `/api/scores/members/${memberId}/history`,
      { params },
    );
  }
}
