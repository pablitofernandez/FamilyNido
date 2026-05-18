import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CompleteOccurrenceRequest,
  CreateHouseholdTaskRequest,
  DayTasks,
  HouseholdTask,
  SetCompletionAttributionRequest,
  TaskCompletionEntry,
  TaskOccurrence,
  UpdateHouseholdTaskRequest,
} from '../models/household-task';

/** Envelope returned by the paginated GET /api/household-tasks. */
export interface HouseholdTaskListPage {
  items: HouseholdTask[];
  total: number;
  page: number;
  pageSize: number;
}

/** HTTP client for the /api/household-tasks endpoints. Thin passthrough. */
@Injectable({ providedIn: 'root' })
export class HouseholdTasksService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/household-tasks';

  /**
   * GET /api/household-tasks. Returns a paginated envelope; the `memberId`
   * filter returns tasks where the member is either the responsible
   * (singular) or one of the related members (N:M) — both surfaces collapse
   * into a single list, which is what the per-member dashboard wants.
   * `search` is a free-text filter against title, description and category.
   */
  list(options?: {
    includeArchived?: boolean;
    memberId?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<HouseholdTaskListPage> {
    let params = new HttpParams();
    if (options?.includeArchived) {
      params = params.set('includeArchived', 'true');
    }
    if (options?.memberId) {
      params = params.set('memberId', options.memberId);
    }
    if (options?.search && options.search.trim().length > 0) {
      params = params.set('search', options.search.trim());
    }
    if (options?.page) {
      params = params.set('page', String(options.page));
    }
    if (options?.pageSize) {
      params = params.set('pageSize', String(options.pageSize));
    }
    return this.http.get<HouseholdTaskListPage>(this.baseUrl, { params });
  }

  /** GET /api/household-tasks/today */
  today(): Observable<DayTasks> {
    return this.http.get<DayTasks>(`${this.baseUrl}/today`);
  }

  /** GET /api/household-tasks/week?startDate=YYYY-MM-DD */
  week(startDate?: string): Observable<DayTasks[]> {
    const params = startDate ? new HttpParams().set('startDate', startDate) : undefined;
    return this.http.get<DayTasks[]>(`${this.baseUrl}/week`, { params });
  }

  /** GET /api/household-tasks/{id} */
  get(id: string): Observable<HouseholdTask> {
    return this.http.get<HouseholdTask>(`${this.baseUrl}/${id}`);
  }

  /** POST /api/household-tasks */
  create(request: CreateHouseholdTaskRequest): Observable<HouseholdTask> {
    return this.http.post<HouseholdTask>(this.baseUrl, request);
  }

  /** PATCH /api/household-tasks/{id} */
  update(id: string, request: UpdateHouseholdTaskRequest): Observable<HouseholdTask> {
    return this.http.patch<HouseholdTask>(`${this.baseUrl}/${id}`, request);
  }

  /** POST /api/household-tasks/{id}/archive */
  archive(id: string): Observable<HouseholdTask> {
    return this.http.post<HouseholdTask>(`${this.baseUrl}/${id}/archive`, {});
  }

  /** POST /api/household-tasks/{id}/restore */
  restore(id: string): Observable<HouseholdTask> {
    return this.http.post<HouseholdTask>(`${this.baseUrl}/${id}/restore`, {});
  }

  /** DELETE /api/household-tasks/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** POST /api/household-tasks/{id}/occurrences/{date}/complete */
  completeOccurrence(
    id: string,
    date: string,
    body?: CompleteOccurrenceRequest,
  ): Observable<TaskOccurrence> {
    return this.http.post<TaskOccurrence>(
      `${this.baseUrl}/${id}/occurrences/${date}/complete`,
      body ?? {},
    );
  }

  /** POST /api/household-tasks/{id}/occurrences/{date}/undo */
  undoOccurrence(id: string, date: string): Observable<TaskOccurrence> {
    return this.http.post<TaskOccurrence>(
      `${this.baseUrl}/${id}/occurrences/${date}/undo`,
      {},
    );
  }

  /**
   * GET /api/household-tasks/{id}/completions — full per-occurrence history
   * sorted most recent first. Used by the "Historial" panel in the task form
   * so admins can see and re-attribute who did the chore on any past day.
   */
  listCompletions(id: string): Observable<TaskCompletionEntry[]> {
    return this.http.get<TaskCompletionEntry[]>(`${this.baseUrl}/${id}/completions`);
  }

  /**
   * PUT /api/household-tasks/{id}/occurrences/{date}/completion — admin-only
   * upsert that re-attributes (or creates) the completion to a chosen member.
   * Used by the "edit who did this" affordance on completed task rows.
   */
  setCompletionAttribution(
    id: string,
    date: string,
    body: SetCompletionAttributionRequest,
  ): Observable<TaskOccurrence> {
    return this.http.put<TaskOccurrence>(
      `${this.baseUrl}/${id}/occurrences/${date}/completion`,
      body,
    );
  }
}
