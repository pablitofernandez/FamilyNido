using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>Endpoints exposing per-user notification preferences.</summary>
public static class NotificationEndpoints
{
    /// <summary>Registers <c>GET/PUT /api/notifications/preferences</c>.</summary>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapGet("/preferences", GetPreferencesAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPut("/preferences", UpdatePreferencesAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/digest/me", SendMyDigestAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        return app;
    }

    private static async Task<IResult> SendMyDigestAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new SendMyDigest.Command(), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetPreferencesAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMyNotificationPreferences.Query(), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        UpdateMyNotificationPreferences.Command command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }
}
