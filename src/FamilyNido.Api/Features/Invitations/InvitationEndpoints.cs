using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>REST endpoints for invitations (RF-AUTH-003).</summary>
public static class InvitationEndpoints
{
    /// <summary>Registers <c>/api/invitations</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/invitations")
            .WithTags("Invitations")
            .RequireAuthorization(Policies.Admin);

        admin.MapGet("/", ListAsync);
        admin.MapPost("/", CreateAsync);
        admin.MapPost("/{id:guid}/revoke", RevokeAsync);

        // Preview + accept-local are anonymous so the recipient can see who
        // is inviting them and onboard with a brand-new password — no
        // PocketID account required.
        var anon = app.MapGroup("/api/invitations")
            .WithTags("Invitations");
        anon.MapGet("/{token}", PreviewAsync).AllowAnonymous();
        anon.MapPost("/{token}/accept-local", AcceptLocalAsync).AllowAnonymous();

        // Accept-oidc requires authentication: any logged-in user (including
        // freshly-orphan ones whose only role is Guest) can claim the
        // invitation. The handler enforces "user not already linked".
        var redeem = app.MapGroup("/api/invitations")
            .WithTags("Invitations")
            .RequireAuthorization(Policies.AuthenticatedUser);
        redeem.MapPost("/{token}/accept-oidc", AcceptOidcAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListInvitations.Query(), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(
        CreateInvitation.Command command,
        IValidator<CreateInvitation.Command> validator,
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
            ? Results.Created($"/api/invitations/{result.Value.Invitation.Id}", result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> RevokeAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new RevokeInvitation.Command(id), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> PreviewAsync(string token, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetInvitationByToken.Query(token), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> AcceptOidcAsync(string token, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new AcceptInvitationOidc.Command(token), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> AcceptLocalAsync(
        string token,
        AcceptLocalBody body,
        IValidator<AcceptInvitationLocal.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AcceptInvitationLocal.Command(token, body.Password);

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

/// <summary>Wire payload for <c>POST /api/invitations/{token}/accept-local</c>.</summary>
/// <param name="Password">Password the recipient chose for their new account.</param>
public sealed record AcceptLocalBody(string Password);
