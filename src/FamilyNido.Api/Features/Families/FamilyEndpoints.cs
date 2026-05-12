using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.Families;

/// <summary>Endpoints exposing the family profile (read for everyone, mutate for admins).</summary>
public static class FamilyEndpoints
{
    /// <summary>Registers <c>GET /api/family</c> and <c>PUT /api/family/location</c>.</summary>
    public static IEndpointRouteBuilder MapFamilyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/family").WithTags("Family");

        group.MapGet("/", GetAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPut("/location", UpdateLocationAsync).RequireAuthorization(Policies.AuthenticatedUser);

        return app;
    }

    private static async Task<IResult> GetAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMyFamily.Query(), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateLocationAsync(
        UpdateFamilyLocation.Command command,
        IValidator<UpdateFamilyLocation.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }
}
