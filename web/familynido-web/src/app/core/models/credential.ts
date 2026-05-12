/** How a credential proves the caller's identity. */
export type CredentialProvider = 'Oidc' | 'Local';

/** Read model returned by `GET /api/auth/credentials`. */
export interface Credential {
  id: string;
  provider: CredentialProvider;
  createdAt: string;
  lastUsedAt: string | null;
  /** Last 6 chars of the OIDC subject — UX disambiguation only. Null for Local. */
  providerKeyHint: string | null;
}

/** Body of `POST /api/auth/local/set-password`. */
export interface SetLocalPasswordRequest {
  /** Required when rotating an existing local credential; ignored on first-time setup. */
  currentPassword: string | null;
  newPassword: string;
}
