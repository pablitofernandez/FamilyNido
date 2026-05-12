using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.School;

/// <summary>
/// Lightweight school "card" for a single <see cref="FamilyMember"/>: centro,
/// curso, tutor and miscellaneous notes plus the typical bus arrival time used
/// by the dashboard widget. One row per member at most — the profile is
/// created lazily the first time it's saved.
/// </summary>
public sealed class SchoolProfile : AuditableEntity
{
    /// <summary>Member this profile belongs to. Unique-indexed below.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the owning <see cref="FamilyMember"/>.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Name of the school ("CEIP Las Acacias"), null when unknown.</summary>
    public string? SchoolName { get; set; }

    /// <summary>Course / level ("3º Primaria"), null when unknown.</summary>
    public string? Grade { get; set; }

    /// <summary>Tutor name (one line). Null when unknown.</summary>
    public string? Tutor { get; set; }

    /// <summary>
    /// How the kid commutes to / from this centre. Defaults to
    /// <see cref="TransportMode.None"/> so a freshly created profile doesn't
    /// commit to anything until the family chooses.
    /// </summary>
    public TransportMode TransportMode { get; set; } = TransportMode.None;

    /// <summary>
    /// Typical local time the kid arrives at the centre in the morning.
    /// Used by the dashboard / digest to render "lleva a las 09:00" hints.
    /// Null when not relevant (e.g. <see cref="TransportMode.Bus"/> kids that
    /// only care about the afternoon pickup).
    /// </summary>
    public TimeOnly? MorningTime { get; set; }

    /// <summary>
    /// Typical local time the kid is picked up in the afternoon. Replaces the
    /// previous <c>BusArrivalTime</c> field — kept the same column post-rename
    /// so the existing data carries over.
    /// </summary>
    public TimeOnly? AfternoonTime { get; set; }

    /// <summary>Free-form notes (allergies known by school, special arrangements, …).</summary>
    public string? Notes { get; set; }
}
