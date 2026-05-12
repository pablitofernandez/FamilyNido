using System.Text.Json.Serialization;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Token endpoint response from <c>https://oauth2.googleapis.com/token</c>. Only
/// the fields we actually consume are surfaced.
/// </summary>
public sealed class GoogleTokenResponse
{
    /// <summary>Short-lived OAuth access token (typically 1 hour).</summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    /// <summary>
    /// Long-lived refresh token. Only returned on the first consent (or when
    /// <c>prompt=consent</c> forces re-issuance). Persisted encrypted.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>Identity token containing the verified email and display name.</summary>
    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    /// <summary>Token lifetime in seconds.</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    /// <summary>Granted scope (space-separated).</summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}
