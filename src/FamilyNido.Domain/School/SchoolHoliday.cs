using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.School;

/// <summary>
/// Family-wide school holiday range. Cancels every bus pickup and every
/// extracurricular session whose date falls inside <see cref="StartDate"/>..<see cref="EndDate"/>
/// (inclusive). One-day holidays are stored with the same start and end.
/// </summary>
public sealed class SchoolHoliday : AuditableEntity
{
    /// <summary>Family this holiday belongs to (authorisation boundary).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Inclusive start date of the holiday range.</summary>
    public required DateOnly StartDate { get; set; }

    /// <summary>Inclusive end date — equal to <see cref="StartDate"/> for one-day holidays.</summary>
    public required DateOnly EndDate { get; set; }

    /// <summary>Human label ("Vacaciones de Navidad", "Día del Pilar"…).</summary>
    public required string Label { get; set; }
}
