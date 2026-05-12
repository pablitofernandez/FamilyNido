using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Domain.School;
using FluentValidation;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// HTTP surface of the Cole module. Like the Salud module, every route is
/// gated to <see cref="Policies.Adult"/> so guests never see school logistics.
/// </summary>
public static class SchoolEndpoints
{
    /// <summary>Registers the <c>/api/school/*</c> routes on the given builder.</summary>
    public static IEndpointRouteBuilder MapSchoolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/school").WithTags("School");

        group.MapGet("/overview", GetOverviewAsync).RequireAuthorization(Policies.Adult);

        group.MapGet("/members/{memberId:guid}/profile", GetProfileAsync).RequireAuthorization(Policies.Adult);
        group.MapPut("/members/{memberId:guid}/profile", UpsertProfileAsync).RequireAuthorization(Policies.Adult);

        group.MapPut("/members/{memberId:guid}/day-schedule", ReplaceDayScheduleAsync).RequireAuthorization(Policies.Adult);

        group.MapPut("/day-schedule/exceptions/{memberId:guid}/{date}", SetDayExceptionAsync).RequireAuthorization(Policies.Adult);
        group.MapDelete("/day-schedule/exceptions/{memberId:guid}/{date}", RemoveDayExceptionAsync).RequireAuthorization(Policies.Adult);

        group.MapPost("/holidays", AddHolidayAsync).RequireAuthorization(Policies.Adult);
        group.MapPut("/holidays/{id:guid}", UpdateHolidayAsync).RequireAuthorization(Policies.Adult);
        group.MapDelete("/holidays/{id:guid}", DeleteHolidayAsync).RequireAuthorization(Policies.Adult);

        group.MapGet("/extracurriculars", ListExtracurricularsAsync).RequireAuthorization(Policies.Adult);
        group.MapPost("/extracurriculars", AddExtracurricularAsync).RequireAuthorization(Policies.Adult);
        group.MapPut("/extracurriculars/{id:guid}", UpdateExtracurricularAsync).RequireAuthorization(Policies.Adult);
        group.MapDelete("/extracurriculars/{id:guid}", DeleteExtracurricularAsync).RequireAuthorization(Policies.Adult);
        group.MapPatch("/extracurriculars/{id:guid}/archive", ArchiveExtracurricularAsync).RequireAuthorization(Policies.Adult);
        group.MapPatch("/extracurriculars/{id:guid}/restore", RestoreExtracurricularAsync).RequireAuthorization(Policies.Adult);
        group.MapPut("/extracurriculars/{id:guid}/exceptions/{date}", SetExtracurricularExceptionAsync).RequireAuthorization(Policies.Adult);
        group.MapDelete("/extracurriculars/{id:guid}/exceptions/{date}", RemoveExtracurricularExceptionAsync).RequireAuthorization(Policies.Adult);

        return app;
    }

    private static async Task<IResult> GetOverviewAsync(
        DateOnly from,
        DateOnly to,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetSchoolOverview.Query(from, to), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetProfileAsync(Guid memberId, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetSchoolProfile.Query(memberId), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpsertProfileAsync(
        Guid memberId,
        SchoolProfileBody body,
        IValidator<UpsertSchoolProfile.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpsertSchoolProfile.Command(
            memberId,
            body.SchoolName,
            body.Grade,
            body.Tutor,
            body.TransportMode,
            body.MorningTime,
            body.AfternoonTime,
            body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ReplaceDayScheduleAsync(
        Guid memberId,
        ReplaceScheduleBody body,
        IValidator<ReplaceSchoolDaySchedule.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ReplaceSchoolDaySchedule.Command(memberId, body.Slots);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> SetDayExceptionAsync(
        Guid memberId,
        DateOnly date,
        DayExceptionBody body,
        IValidator<SetSchoolDayException.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new SetSchoolDayException.Command(
            memberId,
            date,
            body.IsCancelled,
            body.DropoffMemberId,
            body.PickupMemberId,
            body.MorningTime,
            body.AfternoonTime,
            body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> RemoveDayExceptionAsync(
        Guid memberId,
        DateOnly date,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new RemoveSchoolDayException.Command(memberId, date), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> AddHolidayAsync(
        HolidayBody body,
        IValidator<AddSchoolHoliday.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AddSchoolHoliday.Command(body.StartDate, body.EndDate, body.Label);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateHolidayAsync(
        Guid id,
        HolidayBody body,
        IValidator<UpdateSchoolHoliday.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateSchoolHoliday.Command(id, body.StartDate, body.EndDate, body.Label);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteHolidayAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteSchoolHoliday.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    /// <summary>Body for the school-profile upsert.</summary>
    public sealed record SchoolProfileBody(
        string? SchoolName,
        string? Grade,
        string? Tutor,
        TransportMode TransportMode,
        TimeOnly? MorningTime,
        TimeOnly? AfternoonTime,
        string? Notes);

    /// <summary>Body for the school-day schedule replace.</summary>
    public sealed record ReplaceScheduleBody(IReadOnlyList<SchoolDayScheduleSlotDto> Slots);

    /// <summary>Body for the school-day exception (drop-off + pick-up overrides or cancellation).</summary>
    public sealed record DayExceptionBody(
        bool IsCancelled,
        Guid? DropoffMemberId,
        Guid? PickupMemberId,
        TimeOnly? MorningTime,
        TimeOnly? AfternoonTime,
        string? Notes);

    /// <summary>Body shared by add/update holiday.</summary>
    public sealed record HolidayBody(DateOnly StartDate, DateOnly EndDate, string Label);

    private static async Task<IResult> ListExtracurricularsAsync(
        bool? includeArchived,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListExtracurriculars.Query(includeArchived ?? false), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> AddExtracurricularAsync(
        ExtracurricularBody body,
        IValidator<AddExtracurricular.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AddExtracurricular.Command(
            body.MemberId, body.Name, body.Location, body.ContactPhone,
            body.WeeklyDays, body.StartTime, body.EndTime, body.StartDate, body.EndDate,
            body.DefaultDropoffMemberId, body.DefaultPickupMemberId, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateExtracurricularAsync(
        Guid id,
        ExtracurricularBody body,
        IValidator<UpdateExtracurricular.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateExtracurricular.Command(
            id, body.MemberId, body.Name, body.Location, body.ContactPhone,
            body.WeeklyDays, body.StartTime, body.EndTime, body.StartDate, body.EndDate,
            body.DefaultDropoffMemberId, body.DefaultPickupMemberId, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ArchiveExtracurricularAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ArchiveExtracurricular.Command(id, IsArchived: true), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> RestoreExtracurricularAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ArchiveExtracurricular.Command(id, IsArchived: false), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteExtracurricularAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteExtracurricular.Command(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    private static async Task<IResult> SetExtracurricularExceptionAsync(
        Guid id,
        DateOnly date,
        ExtracurricularExceptionBody body,
        IValidator<SetExtracurricularException.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new SetExtracurricularException.Command(
            id, date, body.IsCancelled, body.DropoffMemberId, body.PickupMemberId, body.Notes);
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null) return validation;
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> RemoveExtracurricularExceptionAsync(
        Guid id,
        DateOnly date,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new RemoveExtracurricularException.Command(id, date), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }

    /// <summary>Body shared by add/update extracurricular.</summary>
    public sealed record ExtracurricularBody(
        Guid MemberId,
        string Name,
        string? Location,
        string? ContactPhone,
        Domain.HouseholdTasks.DayOfWeekMask WeeklyDays,
        TimeOnly StartTime,
        TimeOnly EndTime,
        DateOnly StartDate,
        DateOnly? EndDate,
        Guid? DefaultDropoffMemberId,
        Guid? DefaultPickupMemberId,
        string? Notes);

    /// <summary>Body shared by add/update extracurricular exception.</summary>
    public sealed record ExtracurricularExceptionBody(
        bool IsCancelled,
        Guid? DropoffMemberId,
        Guid? PickupMemberId,
        string? Notes);
}
