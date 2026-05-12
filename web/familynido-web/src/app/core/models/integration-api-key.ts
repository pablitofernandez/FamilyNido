/** Persisted projection of an integration API key (no plaintext token). */
export interface IntegrationApiKey {
  id: string;
  name: string;
  /** Public-safe visual prefix (first chars of the secret), e.g. "bxn_a1b2c3d4". */
  prefix: string;
  createdAt: string;
  /** ISO timestamp of last successful auth, null while unused. */
  lastUsedAt: string | null;
  /** ISO timestamp when the token was revoked, null while active. */
  revokedAt: string | null;
}

/** Response of POST /api/integrations/api-keys — token shown once. */
export interface CreatedIntegrationApiKey {
  /** Plaintext secret. The UI must let the user copy it before reload wipes it. */
  token: string;
  key: IntegrationApiKey;
}
