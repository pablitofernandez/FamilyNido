using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Health;

/// <summary>
/// Active or past medication taken by a <see cref="FamilyMember"/>. Stores
/// only what a caretaker needs to know at a glance — name, dose, frequency,
/// start/end and free-form instructions. The module does not orchestrate
/// reminders directly; that's the role of the household-tasks module
/// (a "give Bob the antibiotic" recurring task references this row by name).
/// </summary>
public sealed class Medication : AuditableEntity
{
    /// <summary>Owning family member.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the owning <see cref="FamilyMember"/>.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Trade or generic name of the medication.</summary>
    public required string Name { get; set; }

    /// <summary>Free-text dose ("5 ml", "1 comprimido"…). Null when not applicable.</summary>
    public string? Dose { get; set; }

    /// <summary>Free-text frequency ("cada 8 h", "una vez al día"…).</summary>
    public string? Frequency { get; set; }

    /// <summary>Start date of the treatment.</summary>
    public required DateOnly StartDate { get; set; }

    /// <summary>End date when the treatment is finite (null = ongoing).</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Free-text instructions ("con el desayuno", "no mezclar con leche"…).</summary>
    public string? Instructions { get; set; }
}
