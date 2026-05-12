using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.PublicApi;

/// <summary>
/// Endpoints exposing the versioned public API surface (machine-to-machine).
/// Anything under <c>/api/v1/**</c> is reachable by integrations holding a
/// valid <c>X-Api-Key</c> / <c>Authorization: Bearer …</c> — never by cookie
/// sessions. Versioned from day 1 so a future breaking change can land at
/// <c>/v2</c> without disturbing existing integrators.
/// </summary>
public static class PublicApiEndpoints
{
    /// <summary>Rate-limit policy name applied to every public-api route.</summary>
    public const string RateLimitPolicy = "public-api";

    /// <summary>Registers <c>/api/v1/**</c> routes.</summary>
    public static IEndpointRouteBuilder MapPublicApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("PublicApi")
            .RequireAuthorization(Policies.Integration)
            .RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/tasks", CreateTaskAsync);

        return app;
    }

    private static async Task<IResult> CreateTaskAsync(
        CreateTask.Command command,
        IMediator mediator,
        IValidator<CreateTask.Command> validator,
        CancellationToken ct)
    {
        var problem = await validator.ValidateOrProblemAsync(command, ct);
        if (problem is not null)
        {
            return problem;
        }

        var result = await mediator.SendAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.Error.ToHttpResult();
        }

        return result.Value.Created
            ? Results.Created($"/api/household-tasks/{result.Value.TaskId}", result.Value)
            : Results.Ok(result.Value);
    }
}
