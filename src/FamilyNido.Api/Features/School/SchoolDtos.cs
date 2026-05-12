using FamilyNido.Domain.School;

namespace FamilyNido.Api.Features.School;

/// <summary>Static school card for one member.</summary>
/// <param name="SchoolName">Name of the school / daycare / centre.</param>
/// <param name="Grade">Course / level.</param>
/// <param name="Tutor">Tutor name.</param>
/// <param name="TransportMode">How the kid commutes to / from this centre.</param>
/// <param name="MorningTime">Typical local time the kid arrives at the centre (HH:mm).</param>
/// <param name="AfternoonTime">Typical local time the kid is picked up (HH:mm).</param>
/// <param name="Notes">Free-form notes.</param>
public sealed record SchoolProfileDto(
    string? SchoolName,
    string? Grade,
    string? Tutor,
    TransportMode TransportMode,
    TimeOnly? MorningTime,
    TimeOnly? AfternoonTime,
    string? Notes);

/// <summary>Single weekly slot of the school-day schedule.</summary>
/// <param name="DayOfWeek">Weekday (0 = Sunday … 6 = Saturday, .NET <see cref="System.DayOfWeek"/> ordinals).</param>
/// <param name="DropoffMemberId">Caretaker that takes the kid in the morning, when applicable.</param>
/// <param name="PickupMemberId">Caretaker that picks the kid up in the afternoon, when applicable.</param>
public sealed record SchoolDayScheduleSlotDto(
    DayOfWeek DayOfWeek,
    Guid? DropoffMemberId,
    Guid? PickupMemberId);

/// <summary>Per-date override of the school-day schedule.</summary>
/// <param name="Id">Stable id.</param>
/// <param name="MemberId">Kid the override applies to.</param>
/// <param name="Date">Date of the override.</param>
/// <param name="IsCancelled">True when there's no commute that day.</param>
/// <param name="DropoffMemberId">Caretaker that takes the kid that day, or null.</param>
/// <param name="PickupMemberId">Caretaker that picks the kid up that day, or null.</param>
/// <param name="MorningTime">Override morning entry time for this date, or null when unchanged.</param>
/// <param name="AfternoonTime">Override afternoon exit time for this date, or null when unchanged.</param>
/// <param name="Notes">Optional context note.</param>
public sealed record SchoolDayExceptionDto(
    Guid Id,
    Guid MemberId,
    DateOnly Date,
    bool IsCancelled,
    Guid? DropoffMemberId,
    Guid? PickupMemberId,
    TimeOnly? MorningTime,
    TimeOnly? AfternoonTime,
    string? Notes);

/// <summary>Family-wide school holiday entry.</summary>
public sealed record SchoolHolidayDto(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string Label);

/// <summary>Resolved school day for a single (kid, date) cell shown in the weekly grid.</summary>
/// <param name="MemberId">Kid id.</param>
/// <param name="Date">Date.</param>
/// <param name="TransportMode">Kid's transport mode — drives the dashboard / widget icon.</param>
/// <param name="DropoffMemberId">Resolved drop-off caretaker (override → schedule → null).</param>
/// <param name="PickupMemberId">Resolved pick-up caretaker (override → schedule → null).</param>
/// <param name="MorningTime">Effective entry time (exception override → profile default → null).</param>
/// <param name="AfternoonTime">Effective exit time (exception override → profile default → null).</param>
/// <param name="IsCancelled">True when there's no commute that day (override or holiday).</param>
/// <param name="HolidayLabel">When cancelled by a holiday, the holiday label; otherwise null.</param>
/// <param name="Notes">Override note when applicable.</param>
public sealed record ResolvedSchoolDayDto(
    Guid MemberId,
    DateOnly Date,
    TransportMode TransportMode,
    Guid? DropoffMemberId,
    Guid? PickupMemberId,
    TimeOnly? MorningTime,
    TimeOnly? AfternoonTime,
    bool IsCancelled,
    string? HolidayLabel,
    string? Notes);

/// <summary>Top-level overview returned by GET /api/school/overview.</summary>
/// <param name="From">Inclusive start of the requested range.</param>
/// <param name="To">Inclusive end of the requested range.</param>
/// <param name="Schedule">Persisted weekly school-day pattern (one row per (kid, weekday)).</param>
/// <param name="DayExceptions">School-day exceptions whose date falls in the range.</param>
/// <param name="Holidays">Holiday rows that intersect the range.</param>
/// <param name="ResolvedDays">Day-by-day resolved school-day per kid (cancelled rows included).</param>
/// <param name="Extracurriculars">Active extracurriculars that overlap the range.</param>
/// <param name="ExtracurricularExceptions">Per-date overrides whose date falls in the range.</param>
/// <param name="ResolvedExtracurriculars">Day-by-day resolved sessions in the range.</param>
public sealed record SchoolOverviewDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<KidScheduleDto> Schedule,
    IReadOnlyList<SchoolDayExceptionDto> DayExceptions,
    IReadOnlyList<SchoolHolidayDto> Holidays,
    IReadOnlyList<ResolvedSchoolDayDto> ResolvedDays,
    IReadOnlyList<ExtracurricularDto> Extracurriculars,
    IReadOnlyList<ExtracurricularExceptionDto> ExtracurricularExceptions,
    IReadOnlyList<ResolvedExtracurricularDto> ResolvedExtracurriculars);

/// <summary>Compact view of the weekly schedule grouped by kid.</summary>
public sealed record KidScheduleDto(Guid MemberId, IReadOnlyList<SchoolDayScheduleSlotDto> Slots);
