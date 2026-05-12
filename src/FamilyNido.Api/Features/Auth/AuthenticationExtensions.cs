using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FamilyNido.Api.Features.Integrations;
using FamilyNido.Api.Options;
using FamilyNido.Domain.Families;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FamilyNido.Api.Features.Auth;

/// <summary>Composition of authentication/authorization for the API.</summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Wires the cookie session, the integration API-key handler, the FamilyNido
    /// policy set, and — when an OIDC <see cref="OidcOptions.Authority"/> is
    /// configured — the OpenID Connect challenge handler. The OIDC scheme is
    /// registered conditionally so that a deployment using only local
    /// credentials doesn't trip <c>OpenIdConnectOptions.Validate()</c> on every
    /// request the moment the auth middleware materializes its handlers.
    /// </summary>
    public static IServiceCollection AddFamilyNidoAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        // Peek at the bound options to decide whether the OIDC scheme is on.
        // We can't resolve IOptions here (the provider isn't built yet), but
        // we can bind a temporary instance from the same section.
        var oidcOptions = new OidcOptions();
        configuration.GetSection(OidcOptions.SectionName).Bind(oidcOptions);
        var oidcEnabled = !string.IsNullOrWhiteSpace(oidcOptions.Authority);

        var authBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                // Only point the challenge/sign-out chain at OIDC when it's
                // actually wired up. With OIDC off, an unauthenticated request
                // to a [Authorize] endpoint stays on the cookie scheme, which
                // emits a 401 (or hits OnRedirectToLogin for non-API paths).
                if (oidcEnabled)
                {
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
                }
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "familynido.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Force the Secure flag in production so the cookie is never
                // sent over plain HTTP; SameAsRequest in dev/Testing keeps the
                // local HTTP loop and the in-process WebApplicationFactory
                // workflow working without TLS.
                options.Cookie.SecurePolicy = environment.IsProduction()
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddScheme<IntegrationApiKeyAuthenticationOptions, IntegrationApiKeyAuthenticationHandler>(
                IntegrationApiKeyDefaults.AuthenticationScheme,
                _ => { });

        if (oidcEnabled)
        {
            authBuilder.AddOpenIdConnect(oidc =>
            {
                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.UsePkce = true;
                // SaveTokens=false: the API never re-uses access/refresh
                // tokens after the initial callback, so persisting them in
                // the session cookie just bloats every request header.
                // Keeping them out shrinks the cookie by 4–8 KB and avoids
                // tripping reverse-proxy header limits on every callback.
                oidc.SaveTokens = false;
                oidc.GetClaimsFromUserInfoEndpoint = true;
                oidc.MapInboundClaims = false;
                oidc.TokenValidationParameters.NameClaimType = "name";
                oidc.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
                oidc.Events.OnTokenValidated = EnrichWithRoleClaimAsync;
            });

            // Apply config values to OpenIdConnectOptions via IConfigureOptions so we
            // don't have to build an intermediate provider.
            services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
                .Configure<IOptions<OidcOptions>>((oidc, options) =>
                {
                    var config = options.Value;
                    oidc.Authority = config.Authority;
                    oidc.ClientId = config.ClientId;
                    oidc.ClientSecret = string.IsNullOrEmpty(config.ClientSecret) ? null : config.ClientSecret;
                    oidc.RequireHttpsMetadata = config.RequireHttpsMetadata;
                    oidc.CallbackPath = config.CallbackPath;
                    oidc.SignedOutCallbackPath = config.SignedOutCallbackPath;
                    oidc.Scope.Clear();
                    foreach (var scope in config.Scopes)
                    {
                        oidc.Scope.Add(scope);
                    }
                });
        }

        services.AddAuthorizationBuilder()
            .AddFamilyNidoPolicies();

        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        return services;
    }

    private static async Task EnrichWithRoleClaimAsync(TokenValidatedContext ctx)
    {
        var identity = (ClaimsIdentity?)ctx.Principal?.Identity;
        var subject = ctx.Principal?.FindFirst("sub")?.Value;

        if (identity is null || subject is null || ctx.Principal is null)
        {
            return;
        }

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));

        // Use ResolveAsync(principal): at this point HttpContext.User is still
        // anonymous, so GetAsync would skip the DB work and the role claim
        // baked into the session cookie would always be Guest.
        var userContext = ctx.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
        var resolved = await userContext.ResolveAsync(ctx.Principal, ctx.HttpContext.RequestAborted);

        var role = resolved?.User.Role ?? FamilyRole.Guest;
        identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));

        // Stamp the cookie with the internal user id so subsequent requests can
        // resolve by primary key instead of joining through credentials. Works
        // for orphan users too: their cookie is valid even without a member,
        // which lets the front route them to the "ask the admin" screen with
        // full identity context.
        if (resolved is not null)
        {
            identity.AddClaim(new Claim(CurrentUserContext.UserIdClaimType, resolved.User.Id.ToString()));
        }
    }
}
