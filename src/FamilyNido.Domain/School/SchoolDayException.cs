using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.School;

/// <summary>
/// Per-date override of the kid's school commute. Either flags the day as
/// cancelled (<see cref="IsCancelled"/> true) or reassigns drop-off and / or
/// pick-up caretakers for that day. Absence of a row means the weekly
/// <see cref="SchoolDaySchedule"/> applies as-is.
/// </summary>
public sealed class SchoolDayException : AuditableEntity
{
    /// <summary>Family this exception belongs to (denormalised for fast queries).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Kid the override applies to.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the kid.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Date the override applies to.</summary>
    public required DateOnly Date { get; set; }

    /// <summary>True when there's no commute that day (e.g. teacher strike, day off).</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Drop-off override that day, when reassigned. Null when cancelled or unchanged.</summary>
    public Guid? DropoffMemberId { get; set; }

    /// <summary>Navigation to the drop-off override caretaker.</summary>
    public FamilyMember? DropoffMember { get; set; }

    /// <summary>Pick-up override that day, when reassigned. Null when cancelled or unchanged.</summary>
    public Guid? PickupMemberId { get; set; }

    /// <summary>Navigation to the pick-up override caretaker.</summary>
    public FamilyMember? PickupMember { get; set; }

    /// <summary>
    /// Optional override of the morning entry time for this single date. Null
    /// falls back to the profile's <see cref="SchoolProfile.MorningTime"/>. Set
    /// when the centre announces a special schedule for the day.
    /// </summary>
    public TimeOnly? MorningTime { get; set; }

    /// <summary>
    /// Optional override of the afternoon exit time for this single date. Null
    /// falls back to the profile's <see cref="SchoolProfile.AfternoonTime"/>.
    /// </summary>
    public TimeOnly? AfternoonTime { get; set; }

    /// <summary>Optional note for context ("le recoge el aitite porque tengo médico").</summary>
    public string? Notes { get; set; }
}
