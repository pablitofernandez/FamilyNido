using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Health;

/// <summary>
/// One vaccination event recorded for a <see cref="FamilyMember"/>. Tracks the
/// administered dose plus, optionally, the date of the next required dose so
/// the dashboard and digest can surface it as a reminder.
/// </summary>
public sealed class Vaccination : AuditableEntity
{
    /// <summary>Owning family member.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the owning <see cref="FamilyMember"/>.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Vaccine name as recorded in the cartilla ("Triple vírica", "Tétanos", …).</summary>
    public required string Name { get; set; }

    /// <summary>Date the dose was administered (in the family's local calendar).</summary>
    public required DateOnly Date { get; set; }

    /// <summary>Optional date of the next required dose. Null when one-shot or unknown.</summary>
    public DateOnly? NextDueDate { get; set; }

    /// <summary>Optional free-text notes (lot number, reaction, doctor, …).</summary>
    public string? Notes { get; set; }
}
