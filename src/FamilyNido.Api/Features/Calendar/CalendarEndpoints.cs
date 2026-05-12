using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>REST endpoints for the calendar mirror (RF-CAL-*).</summary>
public static class CalendarEndpoints
{
    /// <summary>Cookie that round-trips the Google OAuth state between start and callback.</summary>
    public const string OAuthStateCookieName = "FamilyNido.calendar.google.oauth-state";

    /// <summary>Path under which the state cookie is valid.</summary>
    public const string OAuthCookiePath = "/api/calendar/google";

    /// <summary>Registers <c>/api/calendar</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var authenticated = app.MapGroup("/api/calendar")
            .WithTags("Calendar")
            .RequireAuthorization(Policies.AuthenticatedUser);

        authenticated.MapGet("/events", ListEventsAsync);
        authenticated.MapPut("/events/{eventId:guid}/members", SetEventMembersAsync);
        authenticated.MapGet("/accounts", ListAccountsAsync);
        authenticated.MapPatch("/calendars/{id:guid}", UpdateCalendarAsync);
        authenticated.MapDelete("/accounts/{id:guid}", UnlinkAccountAsync);
        authenticated.MapPost("/accounts/{id:guid}/sync", SyncAccountAsync);
        authenticated.MapPost("/google/start", StartLinkAsync);

        // Callback is open: Google redirects the browser here after the user grants
        // consent. The session cookie is presented automatically (SameSite=Lax) and
        // the state cookie is what authenticates the dance — not the API auth cookie.
        // We still want the user to be authenticated to FamilyNido though, so the slice
        // re-validates the user identity inside.
        app.MapGet("/api/calendar/google/callback", HandleCallbackAsync)
            .WithTags("Calendar");

        return app;
    }

    private static async Task<IResult> ListEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid[]? memberIds,
        IValidator<ListEvents.Query> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new ListEvents.Query(from, to, memberIds);
        var validation = await validator.ValidateOrProblemAsync(query, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(query, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ListAccountsAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListLinkedAccounts.Query(), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> SetEventMembersAsync(
        Guid eventId,
        SetCalendarEventMembersBody body,
        IValidator<SetCalendarEventMembers.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new SetCalendarEventMembers.Command(eventId, body.MemberIds ?? []);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateCalendarAsync(
        Guid id,
        UpdateLinkedCalendarBody body,
        IValidator<UpdateLinkedCalendar.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateLinkedCalendar.Command(id, body.IsImported, body.FamilyMemberId);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UnlinkAccountAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new UnlinkGoogleAccount.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> SyncAccountAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new TriggerManualSync.Command(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> StartLinkAsync(
        IMediator mediator,
        HttpContext httpContext,
        IOptions<CalendarOptions> options,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new StartGoogleLink.Command(), ct);
        if (!result.IsSuccess)
        {
            return result.Error.ToHttpResult();
        }

        // Drop the encrypted state into a short-lived cookie scoped to /api/calendar/google
        // so subsequent callback hits carry it back; SameSite=Lax is required for the
        // top-level redirect from Google to attach the cookie.
        httpContext.Response.Cookies.Append(OAuthStateCookieName, result.Value.EncryptedState, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = OAuthCookiePath,
            Expires = result.Value.ExpiresAt,
        });

        return Results.Ok(new { authUrl = result.Value.AuthUrl });
    }

    private static async Task<IResult> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        IMediator mediator,
        HttpContext httpContext,
        IOptions<CalendarOptions> options,
        CancellationToken ct)
    {
        var redirectBase = options.Value.PostAuthRedirectPath;

        // Always wipe the state cookie so an aborted attempt doesn't linger.
        httpContext.Response.Cookies.Delete(OAuthStateCookieName, new CookieOptions { Path = OAuthCookiePath });

        if (!string.IsNullOrEmpty(error))
        {
            return Results.Redirect(AppendError(redirectBase, error));
        }

        if (string.IsNullOrEmpty(code))
        {
            return Results.Redirect(AppendError(redirectBase, "missing_code"));
        }

        var encryptedState = httpContext.Request.Cookies[OAuthStateCookieName];
        var command = new HandleGoogleCallback.Command(code, state, encryptedState);
        var result = await mediator.SendAsync(command, ct);

        if (!result.IsSuccess)
        {
            return Results.Redirect(AppendError(redirectBase, result.Error.Code));
        }

        return Results.Redirect(redirectBase + "?linked=" + result.Value.GoogleAccountId);
    }

    private static string AppendError(string basePath, string code)
    {
        var separator = basePath.Contains('?') ? '&' : '?';
        return $"{basePath}{separator}error={Uri.EscapeDataString(code)}";
    }
}

/// <summary>Wire-level body for PATCH /api/calendar/calendars/{id}.</summary>
/// <param name="IsImported">Whether to mirror events from this calendar.</param>
/// <param name="FamilyMemberId">Optional family member to associate (null clears).</param>
public sealed record UpdateLinkedCalendarBody(bool IsImported, Guid? FamilyMemberId);

/// <summary>Wire-level body for PUT /api/calendar/events/{eventId}/members.</summary>
/// <param name="MemberIds">New full set of related members (replaces the previous one). Empty array clears all relations.</param>
public sealed record SetCalendarEventMembersBody(IReadOnlyList<Guid>? MemberIds);
