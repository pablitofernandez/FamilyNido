using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.HouseholdTasks;

namespace FamilyNido.Domain.School;

/// <summary>
/// A recurring after-school activity (inglés, ballet, fútbol…) for one
/// <see cref="FamilyMember"/>. Instances are derived from the
/// <see cref="WeeklyDays"/> bitmask between <see cref="StartDate"/> and
/// <see cref="EndDate"/>; specific dates can override or cancel via
/// <see cref="ExtracurricularException"/>.
/// </summary>
/// <remarks>
/// Drop-off and pick-up caretakers are <see cref="FamilyMember"/> references —
/// grandparents and other non-authenticated caretakers fit naturally as
/// <see cref="MemberType.Other"/>. Both fields are optional because some
/// activities are taken to and from by the same person, or none at all
/// (the kid walks back).
/// </remarks>
public sealed class Extracurricular : AuditableEntity
{
    /// <summary>Family this activity belongs to (denormalised for fast queries).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Kid attending the activity.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the kid.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Activity name shown in the lists ("Inglés", "Ballet").</summary>
    public required string Name { get; set; }

    /// <summary>Optional location ("Centro La Salle", "Polideportivo de Etxarri"…).</summary>
    public string? Location { get; set; }

    /// <summary>Optional contact phone for the activity (academy/coach/centre).</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Bitmask of weekdays the activity occurs (reuses the household-task mask).</summary>
    public required DayOfWeekMask WeeklyDays { get; set; }

    /// <summary>Local start time of each session.</summary>
    public required TimeOnly StartTime { get; set; }

    /// <summary>Local end time of each session.</summary>
    public required TimeOnly EndTime { get; set; }

    /// <summary>First date the activity runs (clamps the recurrence floor).</summary>
    public required DateOnly StartDate { get; set; }

    /// <summary>Last date the activity runs. Null when open-ended.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Default caretaker who takes the kid to the activity. Null when N/A.</summary>
    public Guid? DefaultDropoffMemberId { get; set; }

    /// <summary>Navigation to the drop-off caretaker.</summary>
    public FamilyMember? DefaultDropoffMember { get; set; }

    /// <summary>Default caretaker who picks the kid up after the activity. Null when N/A.</summary>
    public Guid? DefaultPickupMemberId { get; set; }

    /// <summary>Navigation to the pick-up caretaker.</summary>
    public FamilyMember? DefaultPickupMember { get; set; }

    /// <summary>Free-form notes ("traer mochila", "alergia al látex"…).</summary>
    public string? Notes { get; set; }

    /// <summary>Soft archive flag — past courses are preserved for history.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Per-date overrides and cancellations.</summary>
    public ICollection<ExtracurricularException> Exceptions { get; set; } = [];
}
