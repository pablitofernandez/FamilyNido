import { FamilyRole } from './family-member';

/** Lifecycle bucket of an invitation as seen by the preview endpoint. */
export type InvitationStatus = 'Pending' | 'Consumed' | 'Revoked' | 'Expired';

/** Anonymous preview returned by `GET /api/invitations/:token`. */
export interface InvitationPreview {
  familyName: string;
  memberDisplayName: string;
  inviterDisplayName: string | null;
  expiresAt: string;
  status: InvitationStatus;
}

/** Admin read model returned by `GET /api/invitations`. */
export interface Invitation {
  id: string;
  familyMemberId: string;
  memberDisplayName: string;
  email: string;
  roleOnAccept: FamilyRole;
  expiresAt: string;
  createdAt: string;
}

/** Outcome of `POST /api/invitations/:token/accept-oidc` and `accept-local`. */
export interface AcceptInvitationResponse {
  familyMemberId: string;
  familyId: string;
  role: FamilyRole;
}

/** Body of `POST /api/invitations`. */
export interface CreateInvitationRequest {
  memberId: string | null;
  displayName: string | null;
  memberType: 'Adult' | 'Child' | 'Other' | null;
  colorHex: string | null;
  birthDate: string | null;
  email: string;
  roleOnAccept: FamilyRole;
}

/** Response of `POST /api/invitations`. */
export interface CreateInvitationResponse {
  invitation: Invitation;
  memberId: string;
  copyLink: string;
  emailDelivered: boolean;
}
