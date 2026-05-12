using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>REST endpoints for shared household tasks (RF-TASK-*).</summary>
public static class HouseholdTaskEndpoints
{
    /// <summary>Registers <c>/api/household-tasks</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapHouseholdTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/household-tasks")
            .WithTags("HouseholdTasks")
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/", ListAsync);
        group.MapGet("/today", TodayAsync);
        group.MapGet("/week", WeekAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPatch("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/archive", ArchiveAsync);
        group.MapPost("/{id:guid}/restore", RestoreAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/occurrences/{date}/complete", CompleteOccurrenceAsync);
        group.MapPost("/{id:guid}/occurrences/{date}/undo", UndoOccurrenceAsync);
        group.MapPut("/{id:guid}/occurrences/{date}/completion", SetAttributionAsync)
            .RequireAuthorization(Policies.Admin);
        group.MapGet("/{id:guid}/completions", ListCompletionsAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        bool? includeArchived,
        Guid? memberId,
        Guid? assigneeId,
        IMediator mediator,
        CancellationToken ct)
    {
        // Accept the legacy `assigneeId` query param as an alias for `memberId`
        // so old clients keep working during the rollout. Drop after the front
        // is fully migrated.
        var effectiveMemberId = memberId ?? assigneeId;
        var result = await mediator.SendAsync(
            new ListHouseholdTasks.Query(includeArchived ?? false, effectiveMemberId), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> TodayAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetTodayTasks.Query(), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> WeekAsync(
        DateOnly? startDate,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetWeekTasks.Query(startDate), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetHouseholdTask.Query(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(
        CreateHouseholdTask.Command command,
        IValidator<CreateHouseholdTask.Command> validator,
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
            ? Results.Created($"/api/household-tasks/{result.Value.Id}", result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateHouseholdTaskBody body,
        IValidator<UpdateHouseholdTask.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateHouseholdTask.Command(
            id,
            body.Title,
            body.Description,
            body.Category,
            body.Recurrence,
            body.WeeklyDays,
            body.MonthlyDay,
            body.TimeOfDay,
            body.StartDate,
            body.DueDate,
            body.ResponsibleMemberId,
            body.RelatedMemberIds,
            body.IsFloating,
            body.Points);

        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ArchiveAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ArchiveHouseholdTask.Command(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> RestoreAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new RestoreHouseholdTask.Command(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteHouseholdTask.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CompleteOccurrenceAsync(
        Guid id,
        DateOnly date,
        CompleteOccurrenceBody? body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(
            new CompleteOccurrence.Command(id, date, body?.Note), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UndoOccurrenceAsync(
        Guid id,
        DateOnly date,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new UndoOccurrence.Command(id, date), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ListCompletionsAsync(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListTaskCompletions.Query(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> SetAttributionAsync(
        Guid id,
        DateOnly date,
        SetCompletionAttributionBody body,
        IValidator<SetCompletionAttribution.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new SetCompletionAttribution.Command(
            id, date, body.CompletedByMemberId, body.Note);

        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }
}

/// <summary>
/// Wire-level payload for PATCH /api/household-tasks/{id}. The route parameter carries the id.
/// </summary>
/// <param name="Title">Title of the task.</param>
/// <param name="Description">Optional longer description.</param>
/// <param name="Category">Category label.</param>
/// <param name="Recurrence">Recurrence mode.</param>
/// <param name="WeeklyDays">Weekdays when Weekly.</param>
/// <param name="MonthlyDay">Day of month when Monthly.</param>
/// <param name="TimeOfDay">Informative hour.</param>
/// <param name="StartDate">Pivot date.</param>
/// <param name="DueDate">Target date for single-shot tasks.</param>
/// <param name="ResponsibleMemberId">The single member who executes the task. Null leaves it open.</param>
/// <param name="RelatedMemberIds">Members the task concerns.</param>
/// <param name="IsFloating">True for "do me whenever" tasks pending in Hoy until completed once.</param>
/// <param name="Points">Reward (1..10) earned by whoever marks an occurrence done.</param>
public sealed record UpdateHouseholdTaskBody(
    string Title,
    string? Description,
    string? Category,
    Domain.HouseholdTasks.RecurrenceMode Recurrence,
    Domain.HouseholdTasks.DayOfWeekMask? WeeklyDays,
    int? MonthlyDay,
    TimeOnly? TimeOfDay,
    DateOnly StartDate,
    DateOnly? DueDate,
    Guid? ResponsibleMemberId,
    IReadOnlyList<Guid>? RelatedMemberIds,
    bool IsFloating,
    int Points);

/// <summary>Wire-level payload for completing an occurrence; body is optional (used only to attach a note).</summary>
/// <param name="Note">Optional free-text note.</param>
public sealed record CompleteOccurrenceBody(string? Note);

/// <summary>Wire-level payload for the admin attribution PUT.</summary>
/// <param name="CompletedByMemberId">Member who should be credited as the completer.</param>
/// <param name="Note">Optional free-text note (replaces any prior note).</param>
public sealed record SetCompletionAttributionBody(Guid CompletedByMemberId, string? Note);
