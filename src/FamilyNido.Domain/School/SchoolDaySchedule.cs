using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.School;

/// <summary>
/// Weekly default for a kid's commute to / from school on a given weekday.
/// Replaces the bus-only <c>BusPickupSchedule</c>: each row carries an optional
/// drop-off caretaker (who takes the kid in the morning) and an optional
/// pick-up caretaker (who picks them up in the afternoon).
/// </summary>
/// <remarks>
/// <para>
/// Composite key <c>(FamilyMemberId, DayOfWeek)</c>. Both caretaker fields are
/// nullable but at least one must be set — that's enforced at the application
/// layer because the meaningful slot depends on the kid's
/// <see cref="SchoolProfile.TransportMode"/>: a bus kid only fills the pickup,
/// a kid that walks fills both.
/// </para>
/// <para>
/// Caretakers are normal <see cref="FamilyMember"/> rows. Use
/// <see cref="MemberType.Other"/> for grandparents (aitites) so they can be
/// referenced here without polluting the adult/child rosters elsewhere.
/// </para>
/// </remarks>
public sealed class SchoolDaySchedule
{
    /// <summary>Kid the schedule entry refers to.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the kid.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Weekday this entry applies to.</summary>
    public required DayOfWeek DayOfWeek { get; set; }

    /// <summary>Caretaker that takes the kid to the centre in the morning, when applicable.</summary>
    public Guid? DropoffMemberId { get; set; }

    /// <summary>Navigation to the drop-off caretaker.</summary>
    public FamilyMember? DropoffMember { get; set; }

    /// <summary>Caretaker that picks the kid up in the afternoon, when applicable.</summary>
    public Guid? PickupMemberId { get; set; }

    /// <summary>Navigation to the pick-up caretaker.</summary>
    public FamilyMember? PickupMember { get; set; }
}
