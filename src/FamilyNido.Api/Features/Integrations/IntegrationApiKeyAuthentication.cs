using System.Security.Claims;
using System.Text.Encodings.Web;
using FamilyNido.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace FamilyNido.Api.Features.Integrations;

/// <summary>Constants for the integration API-key authentication scheme.</summary>
public static class IntegrationApiKeyDefaults
{
    /// <summary>Auth scheme name used by endpoints under <c>/api/integrations/**</c>.</summary>
    public const string AuthenticationScheme = "IntegrationApiKey";

    /// <summary>Custom header alternative to <c>Authorization: Bearer</c>.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>Bearer scheme prefix recognised in the <c>Authorization</c> header.</summary>
    public const string BearerPrefix = "Bearer ";
}

/// <summary>Custom claim types stamped on the principal authenticated by an API key.</summary>
public static class IntegrationClaimTypes
{
    /// <summary>Family the token belongs to (Guid string).</summary>
    public const string FamilyId = "fn.familyId";

    /// <summary>Member that authored the token (Guid string).</summary>
    public const string AuthorMemberId = "fn.authorMemberId";

    /// <summary>Token row id, useful for audit logging.</summary>
    public const string ApiKeyId = "fn.apiKeyId";
}

/// <summary>Options for <see cref="IntegrationApiKeyAuthenticationHandler"/>. Currently empty.</summary>
public sealed class IntegrationApiKeyAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// Authentication handler that resolves an integration API key from either
/// the <c>Authorization: Bearer …</c> or the <c>X-Api-Key</c> header. Endpoints
/// under <c>/api/integrations/**</c> opt in by declaring this scheme; cookie-
/// authenticated endpoints are unaffected because the default challenge stays
/// on Cookie/OIDC.
/// </summary>
public sealed class IntegrationApiKeyAuthenticationHandler
    : AuthenticationHandler<IntegrationApiKeyAuthenticationOptions>
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Primary constructor.</summary>
    public IntegrationApiKeyAuthenticationHandler(
        IOptionsMonitor<IntegrationApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TimeProvider timeProvider)
        : base(options, logger, encoder)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
        {
            // No header at all → leave the door open for the next scheme.
            return AuthenticateResult.NoResult();
        }

        // Resolve the DbContext from the per-request scope. Constructor injection
        // would tie the (singleton) handler factory to a scoped service.
        var db = Context.RequestServices.GetRequiredService<ApplicationDbContext>();

        var hash = IntegrationTokens.Hash(token);
        var key = await db.IntegrationApiKeys
            .FirstOrDefaultAsync(k => k.TokenHash == hash, Context.RequestAborted);

        if (key is null || key.RevokedAt is not null)
        {
            // Generic message: do not hint whether the token existed and got
            // revoked vs. never existed, to avoid leaking enumeration signal.
            return AuthenticateResult.Fail("Invalid integration API key.");
        }

        // Best-effort last-used update. Failing to write should not block auth
        // — a missed timestamp is preferable to a 401 storm during a DB blip.
        try
        {
            key.LastUsedAt = _timeProvider.GetUtcNow();
            await db.SaveChangesAsync(Context.RequestAborted);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to update LastUsedAt for integration API key {KeyId}", key.Id);
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(IntegrationClaimTypes.FamilyId, key.FamilyId.ToString()));
        identity.AddClaim(new Claim(IntegrationClaimTypes.AuthorMemberId, key.AuthorMemberId.ToString()));
        identity.AddClaim(new Claim(IntegrationClaimTypes.ApiKeyId, key.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, key.Name));
        // NameIdentifier doubles as the audit "actor" because that is the
        // claim the HttpContextActorProvider falls back to. Prefixing keeps
        // these rows visually distinct from human-user audit entries.
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, $"integration:{key.Name}"));

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Match the standard 401 + WWW-Authenticate shape so curl / clients
        // know which scheme they should be using.
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"{Scheme.Name} realm=\"FamilyNido\"";
        return Task.CompletedTask;
    }

    private string? ExtractToken()
    {
        // Header check order: explicit X-Api-Key first (the form HA's
        // rest_command snippet uses), Authorization: Bearer second.
        if (Request.Headers.TryGetValue(IntegrationApiKeyDefaults.HeaderName, out var apiKeyHeader))
        {
            var raw = apiKeyHeader.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }
        }

        if (Request.Headers.TryGetValue(HeaderNames.Authorization, out var authHeader))
        {
            var raw = authHeader.ToString();
            if (raw.StartsWith(IntegrationApiKeyDefaults.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return raw[IntegrationApiKeyDefaults.BearerPrefix.Length..].Trim();
            }
        }

        return null;
    }
}
