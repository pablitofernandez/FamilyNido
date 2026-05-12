using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Auth;

/// <summary>Endpoints for the authentication dance (OIDC, local login, credentials, me).</summary>
public static class AuthEndpoints
{
    /// <summary>Rate-limit policy name applied to <c>POST /api/auth/local/login</c>.</summary>
    public const string LocalLoginRateLimitPolicy = "local-login";

    /// <summary>Registers the /api/auth endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Anonymous discovery endpoint: lets the login screen know whether the
        // OIDC button should be shown. Treats an empty Authority as "OIDC
        // disabled" — which is the default for the dev stack, where the family
        // signs in with local credentials.
        group.MapGet("/providers", (IOptions<OidcOptions> oidc) =>
        {
            var enabled = !string.IsNullOrWhiteSpace(oidc.Value.Authority);
            return Results.Ok(new { oidcEnabled = enabled });
        }).AllowAnonymous();

        // OIDC challenge & cookie sign-out. Returns 404 when no Authority is
        // configured so that a misclick on a cached login button doesn't
        // surface a 500 from the auth middleware.
        group.MapGet("/login", (string? returnUrl, IOptions<OidcOptions> oidc) =>
        {
            if (string.IsNullOrWhiteSpace(oidc.Value.Authority))
            {
                return Results.NotFound(new { error = "oidc_disabled" });
            }
            // returnUrl is reflected into the OIDC AuthenticationProperties and
            // becomes the post-login redirect target. Restrict to local paths
            // ("/something", not "//evil.com" or "https://evil.com") so an
            // attacker can't craft `/api/auth/login?returnUrl=…` that ends up
            // sending the freshly-signed-in user to a phishing page.
            returnUrl = SanitizeLocalReturnUrl(returnUrl);
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                [OpenIdConnectDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        // Drop the local cookie unconditionally. We *don't* drive a federated
        // OIDC sign-out from here: not every session originates from PocketID
        // (local-credentials users have no id_token) and the redirect flow
        // doesn't compose well with the SPA's POST-from-fetch logout. If we
        // ever need full SLO, a separate /logout/federated endpoint can opt
        // into SignOutAsync(OpenIdConnectDefaults...).
        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", GetMeAsync).RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPut("/me/preferred-language", UpdatePreferredLanguageAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        // Local credentials.
        group.MapPost("/local/login", LocalLoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LocalLoginRateLimitPolicy);

        group.MapPost("/local/set-password", SetLocalPasswordAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/credentials", ListCredentialsAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapDelete("/credentials/{id:guid}", RemoveCredentialAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        return app;
    }

    // A return URL is "safe" when it is a same-origin absolute path. Reject
    // schemes, network-relative URLs ("//host"), and anything missing the
    // leading slash; fall back to "/" so the redirect always lands inside
    // the SPA.
    private static string SanitizeLocalReturnUrl(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "/";
        }
        if (!candidate.StartsWith('/') || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }
        return candidate;
    }

    private static async Task<IResult> GetMeAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMe.Query(), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> LocalLoginAsync(
        LocalLogin.Command command,
        IValidator<LocalLogin.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> SetLocalPasswordAsync(
        SetLocalPassword.Command command,
        IValidator<SetLocalPassword.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ListCredentialsAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListMyCredentials.Query(), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> RemoveCredentialAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new RemoveCredential.Command(id), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdatePreferredLanguageAsync(
        UpdateMyPreferredLanguage.Command command,
        IValidator<UpdateMyPreferredLanguage.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }
}
