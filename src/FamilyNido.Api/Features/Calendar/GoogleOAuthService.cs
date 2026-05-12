using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FamilyNido.Api.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Encapsulates the Google OAuth 2.0 Authorization Code dance for the Calendar
/// integration: builds the authorization URL with a CSRF-resistant state, exchanges
/// the redirected code for tokens, and protects/unprotects the refresh token at
/// rest. Stateless — kept as a singleton because it depends only on options and
/// Data Protection, both of which are themselves singletons.
/// </summary>
public sealed class GoogleOAuthService
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string CalendarScope = "https://www.googleapis.com/auth/calendar.readonly";
    private const string OpenIdScope = "openid";
    private const string EmailScope = "email";
    private const string ProfileScope = "profile";

    private readonly IDataProtector _stateProtector;
    private readonly IDataProtector _refreshTokenProtector;
    private readonly IOptions<CalendarOptions> _options;
    private readonly TimeProvider _clock;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Primary constructor.</summary>
    public GoogleOAuthService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<CalendarOptions> options,
        TimeProvider clock,
        IHttpClientFactory httpClientFactory)
    {
        _stateProtector = dataProtectionProvider.CreateProtector("calendar.google.oauth-state");
        _refreshTokenProtector = dataProtectionProvider.CreateProtector("calendar.google.refresh-token");
        _options = options;
        _clock = clock;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Builds the Google authorization URL plus the encrypted state payload that the
    /// API must round-trip via cookie. The plain nonce is what we hand Google through
    /// the <c>state</c> query parameter; the encrypted blob lives in a short-lived
    /// cookie and is re-validated on the callback.
    /// </summary>
    /// <param name="userId">Authenticated FamilyNido user id.</param>
    /// <returns>Tuple containing the auth URL, the encrypted cookie payload, and the cookie expiration.</returns>
    public (string AuthUrl, string EncryptedState, DateTimeOffset ExpiresAt) BuildAuthorizationRequest(Guid userId)
    {
        var options = _options.Value;
        var nonce = Guid.NewGuid().ToString("N");
        var expiresAt = _clock.GetUtcNow().AddMinutes(15);

        var statePayload = new GoogleOAuthState(userId, nonce, expiresAt);
        var encrypted = _stateProtector.Protect(JsonSerializer.Serialize(statePayload));

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = options.GoogleClientId,
            ["redirect_uri"] = options.OAuthRedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', [CalendarScope, OpenIdScope, EmailScope, ProfileScope]),
            // offline + consent guarantees a refresh_token even if the user has linked before.
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = nonce,
        };

        var authUrl = $"{AuthorizationEndpoint}?{BuildQuery(query)}";
        return (authUrl, encrypted, expiresAt);
    }

    /// <summary>
    /// Validates the encrypted state cookie content against the <c>state</c> query
    /// parameter Google echoed back. Returns null when the cookie is missing, expired,
    /// tampered with, or does not match the query nonce.
    /// </summary>
    public GoogleOAuthState? ValidateState(string? encryptedCookie, string? queryStateNonce)
    {
        if (string.IsNullOrEmpty(encryptedCookie) || string.IsNullOrEmpty(queryStateNonce))
        {
            return null;
        }

        try
        {
            var json = _stateProtector.Unprotect(encryptedCookie);
            var payload = JsonSerializer.Deserialize<GoogleOAuthState>(json);
            if (payload is null)
            {
                return null;
            }

            if (payload.ExpiresAt < _clock.GetUtcNow())
            {
                return null;
            }

            if (!CryptographicEquals(payload.Nonce, queryStateNonce))
            {
                return null;
            }

            return payload;
        }
        catch
        {
            // Tampering, key rotation, or malformed payload: treat as invalid.
            return null;
        }
    }

    /// <summary>
    /// Exchanges the authorization <paramref name="code"/> received on the callback
    /// for an access + refresh token pair. Throws on transport or HTTP errors so the
    /// caller can surface a clean failure reason.
    /// </summary>
    public async Task<GoogleTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        using var http = _httpClientFactory.CreateClient(nameof(GoogleOAuthService));

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", options.GoogleClientId),
            new KeyValuePair<string, string>("client_secret", options.GoogleClientSecret),
            new KeyValuePair<string, string>("redirect_uri", options.OAuthRedirectUri),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
        });

        using var response = await http.PostAsync(TokenEndpoint, form, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
        {
            throw new InvalidOperationException("Google token endpoint returned an empty response.");
        }

        return payload;
    }

    /// <summary>
    /// Refreshes the access token for an account using its stored refresh token.
    /// Returns null when Google rejects the refresh (revoked credential), so callers
    /// can flip <c>GoogleAccount.IsRevoked</c> instead of looping forever.
    /// </summary>
    public async Task<GoogleTokenResponse?> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        using var http = _httpClientFactory.CreateClient(nameof(GoogleOAuthService));

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", options.GoogleClientId),
            new KeyValuePair<string, string>("client_secret", options.GoogleClientSecret),
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
        });

        using var response = await http.PostAsync(TokenEndpoint, form, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Google returns 400 invalid_grant for a revoked or expired refresh token.
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
    }

    /// <summary>Encrypts a refresh token for storage in <c>google_accounts.encrypted_refresh_token</c>.</summary>
    public string ProtectRefreshToken(string plaintext) => _refreshTokenProtector.Protect(plaintext);

    /// <summary>Decrypts a refresh token. Throws if the ciphertext was tampered with or the key was rotated past retention.</summary>
    public string UnprotectRefreshToken(string ciphertext) => _refreshTokenProtector.Unprotect(ciphertext);

    /// <summary>
    /// Decodes (without verifying — Google already verified by issuing) the email
    /// and display name from a Google id_token JWT. Used after the code exchange to
    /// label the linked account in the UI without an extra API call.
    /// </summary>
    public static (string Email, string? Name) DecodeIdToken(string idToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value
            ?? throw new InvalidOperationException("Google id_token did not contain an email claim.");
        var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
        return (email, name);
    }

    private static bool CryptographicEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string BuildQuery(IDictionary<string, string?> values)
    {
        var pairs = values
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        return string.Join('&', pairs);
    }
}
