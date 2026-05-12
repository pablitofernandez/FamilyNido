import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CreateFamilyMemberRequest,
  FamilyMember,
  UpdateFamilyMemberRequest,
} from '../models/family-member';

/** HTTP client for the /api/family-members endpoints. Thin passthrough. */
@Injectable({ providedIn: 'root' })
export class FamilyMembersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/family-members';

  /** GET /api/family-members */
  list(options?: { includeInactive?: boolean }): Observable<FamilyMember[]> {
    const params = options?.includeInactive
      ? new HttpParams().set('includeInactive', 'true')
      : undefined;
    return this.http.get<FamilyMember[]>(this.baseUrl, { params });
  }

  /** GET /api/family-members/{id} */
  get(id: string): Observable<FamilyMember> {
    return this.http.get<FamilyMember>(`${this.baseUrl}/${id}`);
  }

  /** POST /api/family-members */
  create(request: CreateFamilyMemberRequest): Observable<FamilyMember> {
    return this.http.post<FamilyMember>(this.baseUrl, request);
  }

  /** PUT /api/family-members/{id} */
  update(id: string, request: UpdateFamilyMemberRequest): Observable<FamilyMember> {
    return this.http.put<FamilyMember>(`${this.baseUrl}/${id}`, request);
  }

  /** PATCH /api/family-members/{id}/deactivate */
  deactivate(id: string): Observable<FamilyMember> {
    return this.http.patch<FamilyMember>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  /** PATCH /api/family-members/{id}/activate */
  activate(id: string): Observable<FamilyMember> {
    return this.http.patch<FamilyMember>(`${this.baseUrl}/${id}/activate`, {});
  }

  /** DELETE /api/family-members/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * POST /api/family-members/{id}/photo as multipart/form-data with a single
   * `file` part. Backend resizes to 512×512 JPEG and returns the updated DTO
   * with the new `photoPath` populated.
   */
  uploadPhoto(id: string, file: File): Observable<FamilyMember> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<FamilyMember>(`${this.baseUrl}/${id}/photo`, form);
  }

  /** DELETE /api/family-members/{id}/photo — clears PhotoPath and removes the file. */
  removePhoto(id: string): Observable<FamilyMember> {
    return this.http.delete<FamilyMember>(`${this.baseUrl}/${id}/photo`);
  }

  /**
   * Builds the URL for the `<img src>` of a member's avatar. The `version`
   * query string busts the browser cache after a fresh upload (since the
   * path is deterministic, the URL would otherwise be identical and the
   * browser would keep showing the old bytes).
   */
  photoUrl(id: string, version?: string | null): string {
    const v = version ? `?v=${encodeURIComponent(version)}` : '';
    return `${this.baseUrl}/${id}/photo${v}`;
  }
}
