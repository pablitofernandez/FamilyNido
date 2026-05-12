using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Agenda;

/// <summary>
/// Per-date deviation from the recurring agenda. Has two flavours:
/// <list type="bullet">
///   <item><b>Override</b> (<see cref="PatternId"/> set): cancels or modifies a
///   specific occurrence of an existing pattern. <see cref="IsCancelled"/>
///   true wipes that day for that pattern; otherwise the non-null fields
///   replace the pattern's defaults for that single day.</item>
///   <item><b>Ad-hoc</b> (<see cref="PatternId"/> null): a one-off entry that
///   doesn't repeat — "hoy también voy a Mondragón aunque no es martes".</item>
/// </list>
/// </summary>
public sealed class MemberAgendaException : AuditableEntity
{
    /// <summary>Family the row belongs to (denormalised for fast queries).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Member the exception applies to.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the member.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Date the exception applies to.</summary>
    public required DateOnly Date { get; set; }

    /// <summary>
    /// When set, this row is an override of <see cref="MemberAgendaPattern"/>
    /// with that id. Null = ad-hoc one-off entry.
    /// </summary>
    public Guid? PatternId { get; set; }

    /// <summary>Navigation to the overridden pattern, when applicable.</summary>
    public MemberAgendaPattern? Pattern { get; set; }

    /// <summary>True when the day cancels the pattern (only valid when <see cref="PatternId"/> is set).</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Override label (or label of the ad-hoc entry).</summary>
    public string? Label { get; set; }

    /// <summary>Override location.</summary>
    public string? Location { get; set; }

    /// <summary>Override start time.</summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>Override end time.</summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>Override transport mode. Null = inherit from the pattern (or unknown for ad-hoc).</summary>
    public AgendaTransportMode? TransportMode { get; set; }

    /// <summary>Override away flag. Null = inherit from the pattern (or true for ad-hoc).</summary>
    public bool? IsAway { get; set; }

    /// <summary>Override notes.</summary>
    public string? Notes { get; set; }
}
