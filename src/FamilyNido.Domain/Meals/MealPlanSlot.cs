using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Meals;

/// <summary>
/// One cell in the weekly meal planner. Holds the free-text name of what the
/// family plans (or planned) to eat at a given <see cref="Date"/> and
/// <see cref="Slot"/>. Each row carries up to two courses (primer and segundo
/// plato); at least one must be non-empty — a row with both courses null is
/// deleted by the application layer instead of being kept around as a tombstone.
/// </summary>
public sealed class MealPlanSlot : AuditableEntity
{
    /// <summary>Family this slot belongs to (authorization boundary).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Date of the slot in the family's local calendar.</summary>
    public required DateOnly Date { get; set; }

    /// <summary>Which slot of the day this row represents.</summary>
    public required MealSlot Slot { get; set; }

    /// <summary>
    /// Primer plato — free-text, 1..120 chars when present. Null when the
    /// family decided to skip a starter and only register a main dish.
    /// </summary>
    public string? FirstCourse { get; set; }

    /// <summary>
    /// Segundo plato — free-text, 1..120 chars when present. Null when only
    /// the starter is registered.
    /// </summary>
    public string? SecondCourse { get; set; }
}
