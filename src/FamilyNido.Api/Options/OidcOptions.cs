namespace FamilyNido.Api.Options;

/// <summary>Strongly-typed binding for the <c>Oidc</c> configuration section.</summary>
public sealed class OidcOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Oidc";

    /// <summary>
    /// OIDC issuer URL (e.g. PocketID). Leave empty to disable the OIDC
    /// login path entirely — the login screen will then offer only local
    /// credentials and <c>/api/auth/login</c> will not be reachable.
    /// </summary>
    public string Authority { get; init; } = "";

    /// <summary>OAuth 2.0 client id registered with the provider.</summary>
    public string ClientId { get; init; } = "";

    /// <summary>Client secret. Leave empty for public PKCE-only clients.</summary>
    public string ClientSecret { get; init; } = "";

    /// <summary>Scopes requested on authentication.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = ["openid", "profile", "email"];

    /// <summary>Relative path where the provider redirects after login.</summary>
    public string CallbackPath { get; init; } = "/signin-oidc";

    /// <summary>Relative path where the provider redirects after logout.</summary>
    public string SignedOutCallbackPath { get; init; } = "/signout-callback-oidc";

    /// <summary>
    /// Whether the metadata document must be served over HTTPS. Defaults to
    /// true. Override to false only if you point <see cref="Authority"/> at a
    /// plain-HTTP local IdP for testing.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;
}
