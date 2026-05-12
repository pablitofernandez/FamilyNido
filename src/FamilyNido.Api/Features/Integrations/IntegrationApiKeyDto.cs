namespace FamilyNido.Api.Features.Integrations;

/// <summary>
/// Wire shape returned when listing existing integration API keys. The
/// plaintext token is intentionally absent — once created it cannot be
/// recovered, callers must regenerate.
/// </summary>
/// <param name="Id">Token row id.</param>
/// <param name="Name">Human-readable label.</param>
/// <param name="Prefix">Public-safe visual prefix (first chars of the secret).</param>
/// <param name="CreatedAt">When the token was created.</param>
/// <param name="LastUsedAt">When the token was last accepted by the auth handler. Null while unused.</param>
/// <param name="RevokedAt">When the token was revoked. Null while active.</param>
public sealed record IntegrationApiKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

/// <summary>
/// Response of the create endpoint — includes the freshly minted plaintext
/// token alongside the persisted projection. The plaintext is the only chance
/// the caller has to copy it to its target system.
/// </summary>
/// <param name="Token">Plaintext secret. Show once, never again.</param>
/// <param name="Key">Persisted token metadata.</param>
public sealed record CreatedIntegrationApiKeyDto(string Token, IntegrationApiKeyDto Key);
