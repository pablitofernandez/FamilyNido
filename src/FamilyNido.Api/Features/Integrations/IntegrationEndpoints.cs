using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FluentValidation;

namespace FamilyNido.Api.Features.Integrations;

/// <summary>
/// Endpoints for admin-side management of integration API keys. The actual
/// machine-to-machine API surface (creating tasks, future endpoints) lives
/// under <c>/api/v1/**</c> in <see cref="PublicApi.PublicApiEndpoints"/>.
/// </summary>
public static class IntegrationEndpoints
{
    /// <summary>Registers <c>/api/integrations/api-keys/**</c> routes.</summary>
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations").WithTags("Integrations");

        // ─── Admin token management (cookie session) ─────────────────────
        group.MapGet("/api-keys", ListAsync)
            .RequireAuthorization(Policies.Admin);

        group.MapPost("/api-keys", CreateAsync)
            .RequireAuthorization(Policies.Admin);

        group.MapPost("/api-keys/{id:guid}/revoke", RevokeAsync)
            .RequireAuthorization(Policies.Admin);

        return app;
    }

    private static async Task<IResult> ListAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListIntegrationApiKeys.Query(), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(
        CreateIntegrationApiKey.Command command,
        IMediator mediator,
        IValidator<CreateIntegrationApiKey.Command> validator,
        CancellationToken ct)
    {
        var problem = await validator.ValidateOrProblemAsync(command, ct);
        if (problem is not null)
        {
            return problem;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Created($"/api/integrations/api-keys/{result.Value.Key.Id}", result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> RevokeAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new RevokeIntegrationApiKey.Command(id), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToHttpResult();
    }
}
