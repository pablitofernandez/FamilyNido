using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.School;

/// <summary>
/// Per-date override of an <see cref="Extracurricular"/>. Either flags the
/// session as cancelled (<see cref="IsCancelled"/> true) — useful when the
/// course skips a day for a specific reason that doesn't match a global
/// holiday — or reassigns the drop-off / pick-up caretaker for that day.
/// </summary>
public sealed class ExtracurricularException : AuditableEntity
{
    /// <summary>Owning extracurricular activity.</summary>
    public required Guid ExtracurricularId { get; set; }

    /// <summary>Navigation to the owning <see cref="Extracurricular"/>.</summary>
    public Extracurricular? Extracurricular { get; set; }

    /// <summary>Date the override applies to.</summary>
    public required DateOnly Date { get; set; }

    /// <summary>True when the session is cancelled that day.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Drop-off caretaker that day, or null when unchanged / cancelled.</summary>
    public Guid? DropoffMemberId { get; set; }

    /// <summary>Navigation to the drop-off override caretaker.</summary>
    public FamilyMember? DropoffMember { get; set; }

    /// <summary>Pick-up caretaker that day, or null when unchanged / cancelled.</summary>
    public Guid? PickupMemberId { get; set; }

    /// <summary>Navigation to the pick-up override caretaker.</summary>
    public FamilyMember? PickupMember { get; set; }

    /// <summary>Optional context note.</summary>
    public string? Notes { get; set; }
}
