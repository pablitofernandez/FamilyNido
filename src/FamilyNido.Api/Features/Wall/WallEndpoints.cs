using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.Wall;

/// <summary>REST endpoints for the wall (RF-WALL-*).</summary>
public static class WallEndpoints
{
    /// <summary>Registers <c>/api/wall</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapWallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wall")
            .WithTags("Wall")
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/messages", ListAsync);
        group.MapGet("/messages/{id:guid}", GetAsync);
        group.MapPost("/messages", CreateAsync);
        group.MapPatch("/messages/{id:guid}", UpdateAsync);
        group.MapDelete("/messages/{id:guid}", DeleteAsync);
        group.MapPost("/messages/{id:guid}/pin", PinAsync);
        group.MapPost("/messages/{id:guid}/unpin", UnpinAsync);
        group.MapPost("/messages/{id:guid}/comments", AddCommentAsync);
        group.MapDelete("/comments/{id:guid}", DeleteCommentAsync);
        group.MapPost("/messages/{id:guid}/reactions", ToggleReactionAsync);
        group.MapGet("/unread-count", UnreadCountAsync);
        group.MapPatch("/last-read", LastReadAsync);
        group.MapPost("/preview", PreviewAsync);

        return app;
    }

    private static async Task<IResult> PreviewAsync(
        PreviewWallMarkdown.Command command,
        IValidator<PreviewWallMarkdown.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ListAsync(
        DateTimeOffset? before,
        int? limit,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListWallMessages.Query(before, limit), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetWallMessage.Query(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(
        CreateWallMessage.Command command,
        IValidator<CreateWallMessage.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Created($"/api/wall/messages/{result.Value.Id}", result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateWallMessageBody body,
        IValidator<UpdateWallMessage.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateWallMessage.Command(id, body.Text, body.ImageFileId);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteWallMessage.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> PinAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new PinWallMessage.Command(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UnpinAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new UnpinWallMessage.Command(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> AddCommentAsync(
        Guid id,
        AddWallCommentBody body,
        IValidator<AddWallComment.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AddWallComment.Command(id, body.Text);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Created($"/api/wall/comments/{result.Value.Id}", result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteCommentAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteWallComment.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ToggleReactionAsync(
        Guid id,
        ToggleReactionBody body,
        IValidator<ToggleWallReaction.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ToggleWallReaction.Command(id, body.Emoji);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UnreadCountAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetWallUnreadCount.Query(), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> LastReadAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new UpdateWallLastRead.Command(), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }
}

/// <summary>Wire payload for PATCH <c>/api/wall/messages/{id}</c>.</summary>
/// <param name="Text">New markdown source.</param>
/// <param name="ImageFileId">Optional image attachment.</param>
public sealed record UpdateWallMessageBody(string Text, Guid? ImageFileId);

/// <summary>Wire payload for POST <c>/api/wall/messages/{id}/comments</c>.</summary>
/// <param name="Text">Comment text as markdown.</param>
public sealed record AddWallCommentBody(string Text);

/// <summary>Wire payload for POST <c>/api/wall/messages/{id}/reactions</c>.</summary>
/// <param name="Emoji">Emoji to toggle.</param>
public sealed record ToggleReactionBody(string Emoji);
