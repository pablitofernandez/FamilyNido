using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;

namespace FamilyNido.Api.Features.Health;

/// <summary>
/// Endpoints for the health module. Gated to Admin/Adult roles — Guests
/// (children with their own accounts in the future) never see this surface.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Registers <c>/api/health/*</c> routes on the given builder.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health").WithTags("Health");

        group.MapGet("/members/{memberId:guid}", GetMemberAsync)
            .RequireAuthorization(Policies.Adult);

        group.MapPut("/members/{memberId:guid}/profile", UpsertProfileAsync)
            .RequireAuthorization(Policies.Adult);

        group.MapPost("/members/{memberId:guid}/vaccinations", AddVaccinationAsync)
            .RequireAuthorization(Policies.Adult);

        group.MapPut("/vaccinations/{id:guid}", UpdateVaccinationAsync)
            .RequireAuthorization(Policies.Adult);

        group.MapDelete("/vaccinations/{id:guid}", DeleteVaccinationAsync)
            .RequireAuthorization(Policies.Adult);

        group.MapPost("/members/{memberId:guid}/medications", AddMedicationAsync)
            .RequireAuthorization(Policies.Adult);

        group.MapPut("/medications/{id:guid}", UpdateMedicationAsync)
            .RequireAuthorization(Policies.Adult);

        group.MapDelete("/medications/{id:guid}", DeleteMedicationAsync)
            .RequireAuthorization(Policies.Adult);

        return app;
    }

    private static async Task<IResult> GetMemberAsync(Guid memberId, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMemberHealth.Query(memberId), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpsertProfileAsync(
        Guid memberId,
        UpsertHealthProfileBody body,
        IValidator<UpsertHealthProfile.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpsertHealthProfile.Command(
            memberId, body.BloodType, body.Allergies, body.ChronicConditions, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> AddVaccinationAsync(
        Guid memberId,
        VaccinationBody body,
        IValidator<AddVaccination.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AddVaccination.Command(memberId, body.Name, body.Date, body.NextDueDate, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateVaccinationAsync(
        Guid id,
        VaccinationBody body,
        IValidator<UpdateVaccination.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateVaccination.Command(id, body.Name, body.Date, body.NextDueDate, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteVaccinationAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteVaccination.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> AddMedicationAsync(
        Guid memberId,
        MedicationBody body,
        IValidator<AddMedication.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AddMedication.Command(
            memberId, body.Name, body.Dose, body.Frequency, body.StartDate, body.EndDate, body.Instructions);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateMedicationAsync(
        Guid id,
        MedicationBody body,
        IValidator<UpdateMedication.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateMedication.Command(
            id, body.Name, body.Dose, body.Frequency, body.StartDate, body.EndDate, body.Instructions);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteMedicationAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteMedication.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    /// <summary>Body for the health-profile upsert (memberId comes from the route).</summary>
    public sealed record UpsertHealthProfileBody(
        string? BloodType,
        string? Allergies,
        string? ChronicConditions,
        string? Notes);

    /// <summary>Body shared by add/update vaccination (memberId/id come from the route).</summary>
    public sealed record VaccinationBody(
        string Name,
        DateOnly Date,
        DateOnly? NextDueDate,
        string? Notes);

    /// <summary>Body shared by add/update medication.</summary>
    public sealed record MedicationBody(
        string Name,
        string? Dose,
        string? Frequency,
        DateOnly StartDate,
        DateOnly? EndDate,
        string? Instructions);
}
