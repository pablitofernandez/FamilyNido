import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Credential, SetLocalPasswordRequest } from '../models/credential';

/** Thin HTTP wrapper over /api/auth/credentials and /api/auth/local/set-password. */
@Injectable({ providedIn: 'root' })
export class CredentialsService {
  private readonly http = inject(HttpClient);

  list(): Observable<Credential[]> {
    return this.http.get<Credential[]>('/api/auth/credentials');
  }

  setPassword(payload: SetLocalPasswordRequest): Observable<void> {
    return this.http.post<void>('/api/auth/local/set-password', payload);
  }

  remove(credentialId: string): Observable<void> {
    return this.http.delete<void>(`/api/auth/credentials/${encodeURIComponent(credentialId)}`);
  }
}
