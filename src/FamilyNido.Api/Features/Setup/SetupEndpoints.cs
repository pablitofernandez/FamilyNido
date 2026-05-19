using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.Setup;

/// <summary>
/// Anonymous bootstrap endpoints (issue #20). Two routes that let a
/// brand-new self-hosted instance create its first admin from the SPA
/// without anyone having to ssh into the database or wire up OIDC first.
/// </summary>
public static class SetupEndpoints
{
    /// <summary>Registers the <c>/api/setup</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/setup")
            .WithTags("Setup");

        // Both routes are anonymous on purpose: the SPA hits status before
        // it has a session, and the bootstrap call IS the path to getting
        // the first session. The handlers themselves enforce the "only
        // before any user exists" invariant.
        group.MapGet("/status", StatusAsync).AllowAnonymous();
        group.MapPost("/initial-admin", InitialAdminAsync).AllowAnonymous();

        return app;
    }

    private static async Task<IResult> StatusAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetSetupStatus.Query(), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> InitialAdminAsync(
        InitializeAdmin.Command command,
        IValidator<InitializeAdmin.Command> validator,
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
}
