import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AcceptInvitationResponse,
  CreateInvitationRequest,
  CreateInvitationResponse,
  Invitation,
  InvitationPreview,
} from '../models/invitation';

/** Thin HTTP wrapper over /api/invitations. */
@Injectable({ providedIn: 'root' })
export class InvitationsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/invitations';

  /** Anonymous: read invitation metadata by raw token from the URL. */
  preview(token: string): Observable<InvitationPreview> {
    return this.http.get<InvitationPreview>(`${this.base}/${encodeURIComponent(token)}`);
  }

  /** Authenticated: redeem the invitation using the current OIDC session. */
  acceptOidc(token: string): Observable<AcceptInvitationResponse> {
    return this.http.post<AcceptInvitationResponse>(
      `${this.base}/${encodeURIComponent(token)}/accept-oidc`,
      {});
  }

  /** Anonymous: redeem the invitation by setting a brand-new local password. */
  acceptLocal(token: string, password: string): Observable<AcceptInvitationResponse> {
    return this.http.post<AcceptInvitationResponse>(
      `${this.base}/${encodeURIComponent(token)}/accept-local`,
      { password });
  }

  /** Admin: list pending invitations of the caller's family. */
  list(): Observable<Invitation[]> {
    return this.http.get<Invitation[]>(this.base);
  }

  /** Admin: create a new invitation (and optionally a new family member inline). */
  create(payload: CreateInvitationRequest): Observable<CreateInvitationResponse> {
    return this.http.post<CreateInvitationResponse>(this.base, payload);
  }

  /** Admin: revoke a pending invitation. */
  revoke(invitationId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(invitationId)}/revoke`, {});
  }
}
