using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Domain.Agenda;
using FluentValidation;

namespace FamilyNido.Api.Features.MemberAgenda;

/// <summary>HTTP surface of the member-agenda module.</summary>
public static class MemberAgendaEndpoints
{
    /// <summary>Registers the <c>/api/member-agenda/*</c> routes on the given builder.</summary>
    public static IEndpointRouteBuilder MapMemberAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/member-agenda").WithTags("MemberAgenda");

        // Read access for any authenticated user — agenda visibility is family-internal.
        group.MapGet("/overview", GetOverviewAsync).RequireAuthorization(Policies.AuthenticatedUser);

        // Mutations are gated to authenticated user, with admin-or-self enforced inside the slice.
        group.MapPost("/patterns", CreatePatternAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPut("/patterns/{id:guid}", UpdatePatternAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapDelete("/patterns/{id:guid}", DeletePatternAsync).RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/exceptions", CreateExceptionAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapPut("/exceptions/{id:guid}", UpdateExceptionAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapDelete("/exceptions/{id:guid}", DeleteExceptionAsync).RequireAuthorization(Policies.AuthenticatedUser);

        return app;
    }

    private static async Task<IResult> GetOverviewAsync(
        DateOnly from,
        DateOnly to,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMemberAgendaOverview.Query(from, to), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CreatePatternAsync(
        PatternBody body,
        IValidator<CreateMemberAgendaPattern.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new CreateMemberAgendaPattern.Command(
            body.MemberId, body.DayOfWeek, body.Label, body.Location,
            body.StartTime, body.EndTime, body.TransportMode, body.IsAway, body.Notes, body.IsActive);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdatePatternAsync(
        Guid id,
        PatternBody body,
        IValidator<UpdateMemberAgendaPattern.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateMemberAgendaPattern.Command(
            id, body.DayOfWeek, body.Label, body.Location,
            body.StartTime, body.EndTime, body.TransportMode, body.IsAway, body.Notes, body.IsActive);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeletePatternAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteMemberAgendaPattern.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CreateExceptionAsync(
        ExceptionBody body,
        IValidator<SetMemberAgendaException.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new SetMemberAgendaException.Command(
            Id: null,
            body.MemberId, body.Date, body.PatternId, body.IsCancelled, body.Label, body.Location,
            body.StartTime, body.EndTime, body.TransportMode, body.IsAway, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateExceptionAsync(
        Guid id,
        ExceptionBody body,
        IValidator<SetMemberAgendaException.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new SetMemberAgendaException.Command(
            id,
            body.MemberId, body.Date, body.PatternId, body.IsCancelled, body.Label, body.Location,
            body.StartTime, body.EndTime, body.TransportMode, body.IsAway, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteExceptionAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteMemberAgendaException.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    /// <summary>Body for create/update pattern.</summary>
    public sealed record PatternBody(
        Guid MemberId,
        DayOfWeek DayOfWeek,
        string Label,
        string? Location,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        AgendaTransportMode TransportMode,
        bool IsAway,
        string? Notes,
        bool IsActive);

    /// <summary>Body for create/update exception.</summary>
    public sealed record ExceptionBody(
        Guid MemberId,
        DateOnly Date,
        Guid? PatternId,
        bool IsCancelled,
        string? Label,
        string? Location,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        AgendaTransportMode? TransportMode,
        bool? IsAway,
        string? Notes);
}
