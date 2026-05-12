using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Domain.Meals;
using FluentValidation;

namespace FamilyNido.Api.Features.Meals;

/// <summary>REST endpoints for the meal planner (RF-MEAL-*).</summary>
public static class MealEndpoints
{
    /// <summary>Registers <c>/api/meals</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapMealEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meals")
            .WithTags("Meals")
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/week", GetWeekAsync);
        group.MapPut("/slots", UpsertSlotAsync);
        group.MapDelete("/slots", ClearSlotAsync);
        group.MapGet("/suggestions", GetSuggestionsAsync);
        group.MapPost("/week/duplicate-previous", DuplicatePreviousAsync);

        return app;
    }

    private static async Task<IResult> GetWeekAsync(
        DateOnly? startDate,
        IMediator mediator,
        CancellationToken ct)
    {
        var date = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await mediator.SendAsync(new GetWeekPlan.Query(date), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpsertSlotAsync(
        UpsertMealSlotBody body,
        IValidator<UpsertMealSlot.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpsertMealSlot.Command(body.Date, body.Slot, body.Course, body.Name);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ClearSlotAsync(
        DateOnly date,
        MealSlot slot,
        MealCourse course,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ClearMealSlot.Command(date, slot, course), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetSuggestionsAsync(
        string? prefix,
        int? limit,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMealSuggestions.Query(prefix, limit), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DuplicatePreviousAsync(
        DuplicatePreviousWeekBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(
            new DuplicatePreviousWeek.Command(body.WeekStart, body.Overwrite),
            ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }
}

/// <summary>Wire body for PUT <c>/api/meals/slots</c>.</summary>
/// <param name="Date">Target date.</param>
/// <param name="Slot">Slot of the day.</param>
/// <param name="Course">Course within the slot (First/Second).</param>
/// <param name="Name">New name (1..120 chars).</param>
public sealed record UpsertMealSlotBody(DateOnly Date, MealSlot Slot, MealCourse Course, string Name);

/// <summary>Wire body for POST <c>/api/meals/week/duplicate-previous</c>.</summary>
/// <param name="WeekStart">Any date inside the target week (snapped to Monday).</param>
/// <param name="Overwrite">When true, replaces non-empty slots in the destination week.</param>
public sealed record DuplicatePreviousWeekBody(DateOnly WeekStart, bool Overwrite);
