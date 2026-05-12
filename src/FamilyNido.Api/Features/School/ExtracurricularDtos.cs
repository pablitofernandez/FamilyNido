using FamilyNido.Domain.HouseholdTasks;

namespace FamilyNido.Api.Features.School;

/// <summary>Wire shape of an after-school activity.</summary>
/// <param name="Id">Stable id.</param>
/// <param name="MemberId">Kid attending.</param>
/// <param name="Name">Activity name.</param>
/// <param name="Location">Optional location.</param>
/// <param name="ContactPhone">Optional contact phone (academy / coach / centre).</param>
/// <param name="WeeklyDays">Bitmask of weekdays the activity occurs.</param>
/// <param name="StartTime">Local start time.</param>
/// <param name="EndTime">Local end time.</param>
/// <param name="StartDate">First date the activity runs.</param>
/// <param name="EndDate">Last date the activity runs (open-ended when null).</param>
/// <param name="DefaultDropoffMemberId">Default drop-off caretaker.</param>
/// <param name="DefaultPickupMemberId">Default pick-up caretaker.</param>
/// <param name="Notes">Free-form notes.</param>
/// <param name="IsArchived">Soft-archive flag.</param>
public sealed record ExtracurricularDto(
    Guid Id,
    Guid MemberId,
    string Name,
    string? Location,
    string? ContactPhone,
    DayOfWeekMask WeeklyDays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid? DefaultDropoffMemberId,
    Guid? DefaultPickupMemberId,
    string? Notes,
    bool IsArchived);

/// <summary>Wire shape of a per-date exception.</summary>
public sealed record ExtracurricularExceptionDto(
    Guid Id,
    Guid ExtracurricularId,
    DateOnly Date,
    bool IsCancelled,
    Guid? DropoffMemberId,
    Guid? PickupMemberId,
    string? Notes);

/// <summary>Per-day resolved instance shown in dashboard / weekly view.</summary>
/// <param name="ExtracurricularId">Source activity id.</param>
/// <param name="MemberId">Kid attending.</param>
/// <param name="Date">Date.</param>
/// <param name="StartTime">Local start (from the activity, never overridden in v1).</param>
/// <param name="EndTime">Local end (same).</param>
/// <param name="Name">Activity name.</param>
/// <param name="Location">Location.</param>
/// <param name="ContactPhone">Contact phone.</param>
/// <param name="DropoffMemberId">Resolved drop-off caretaker (override → default → null).</param>
/// <param name="PickupMemberId">Resolved pick-up caretaker (override → default → null).</param>
/// <param name="IsCancelled">True when cancelled by exception or holiday.</param>
/// <param name="HolidayLabel">Holiday label when cancelled by a holiday range.</param>
/// <param name="Notes">Override note when applicable.</param>
public sealed record ResolvedExtracurricularDto(
    Guid ExtracurricularId,
    Guid MemberId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Name,
    string? Location,
    string? ContactPhone,
    Guid? DropoffMemberId,
    Guid? PickupMemberId,
    bool IsCancelled,
    string? HolidayLabel,
    string? Notes);
