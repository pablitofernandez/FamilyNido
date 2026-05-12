/**
 * Kinds of family member. Only {@link MemberType.Adult} members can be linked
 * to a user and authenticate.
 */
export type MemberType = 'Adult' | 'Child' | 'Other';

/** Authorization role of an authenticated user. */
export type FamilyRole = 'Guest' | 'Adult' | 'Admin';

/** Compact pending-invitation summary embedded in {@link FamilyMember}. */
export interface PendingInvitationSummary {
  id: string;
  email: string;
  expiresAt: string;
}

/** Shape returned by `GET /api/family-members` (single item). */
export interface FamilyMember {
  id: string;
  displayName: string;
  memberType: MemberType;
  colorHex: string;
  birthDate: string | null;
  contactEmail: string | null;
  photoPath: string | null;
  isActive: boolean;
  hasAccount: boolean;
  role: FamilyRole | null;
  pendingInvitation: PendingInvitationSummary | null;
}

/** Payload for `POST /api/family-members`. */
export interface CreateFamilyMemberRequest {
  displayName: string;
  memberType: MemberType;
  colorHex: string;
  birthDate?: string | null;
  contactEmail?: string | null;
}

/** Payload for `PUT /api/family-members/{id}`. */
export interface UpdateFamilyMemberRequest {
  displayName: string;
  colorHex: string;
  birthDate?: string | null;
  contactEmail?: string | null;
}
