using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.Dashboard;

/// <summary>Endpoints exposing the dashboard widget layout.</summary>
public static class DashboardEndpoints
{
    /// <summary>Registers <c>GET/PUT /api/dashboard/preferences</c>.</summary>
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        group.MapGet("/preferences", GetPreferencesAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPut("/preferences", UpdatePreferencesAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);

        return app;
    }

    private static async Task<IResult> GetPreferencesAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMyDashboardPreferences.Query(), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        UpdateMyDashboardPreferences.Command command,
        IValidator<UpdateMyDashboardPreferences.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }
}
