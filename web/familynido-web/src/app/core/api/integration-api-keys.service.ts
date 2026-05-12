import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CreatedIntegrationApiKey, IntegrationApiKey } from '../models/integration-api-key';

/** Thin client for `/api/integrations/api-keys` (admin-only). */
@Injectable({ providedIn: 'root' })
export class IntegrationApiKeysService {
  private readonly http = inject(HttpClient);

  /** List the family's tokens, newest first. */
  list(): Observable<IntegrationApiKey[]> {
    return this.http.get<IntegrationApiKey[]>('/api/integrations/api-keys');
  }

  /**
   * Mint a new token. The returned `token` is the only chance to copy the
   * plaintext — once the dialog closes only the digest remains in the DB.
   */
  create(name: string): Observable<CreatedIntegrationApiKey> {
    return this.http.post<CreatedIntegrationApiKey>(
      '/api/integrations/api-keys',
      { name },
    );
  }

  /** Soft-revoke a token. Idempotent. */
  revoke(id: string): Observable<void> {
    return this.http.post<void>(`/api/integrations/api-keys/${id}/revoke`, {});
  }
}
