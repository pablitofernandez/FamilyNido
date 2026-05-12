using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Agenda;

/// <summary>
/// Recurring weekly entry in a family member's agenda — "Alice goes to
/// Mondragón every Tuesday". Multiple rows are allowed for the same
/// (member, weekday) so the same day can carry several activities (e.g. work
/// in the morning + gym in the evening).
/// </summary>
/// <remarks>
/// Per-date deviations (cancellations, time changes, ad-hoc additions) live
/// in <see cref="MemberAgendaException"/>. Resolution at the API layer merges
/// the two layers exactly like the school module does.
/// </remarks>
public sealed class MemberAgendaPattern : AuditableEntity
{
    /// <summary>Family the entry belongs to (denormalised for fast queries).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Member this pattern describes.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the member.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Weekday the pattern applies to.</summary>
    public required DayOfWeek DayOfWeek { get; set; }

    /// <summary>Short label shown in the dashboard widget ("Mondragón", "Gym").</summary>
    public required string Label { get; set; }

    /// <summary>Optional location ("Mondragón", "Polideportivo Atxuri"). Free text.</summary>
    public string? Location { get; set; }

    /// <summary>Optional start time. Null = "all day".</summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>Optional end time. Null = open-ended.</summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>How the member moves that day. <see cref="AgendaTransportMode.None"/> = unspecified.</summary>
    public AgendaTransportMode TransportMode { get; set; } = AgendaTransportMode.None;

    /// <summary>True when the member is physically away from home (used to surface "hoy fuera" on the dashboard).</summary>
    public bool IsAway { get; set; } = true;

    /// <summary>Optional free-text notes ("llevar comida en tupper", "saca el coche del garaje").</summary>
    public string? Notes { get; set; }

    /// <summary>True when the pattern is in force. Soft-disable lets you pause a pattern without losing it.</summary>
    public bool IsActive { get; set; } = true;
}
