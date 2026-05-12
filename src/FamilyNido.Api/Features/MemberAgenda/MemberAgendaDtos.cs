using FamilyNido.Domain.Agenda;

namespace FamilyNido.Api.Features.MemberAgenda;

/// <summary>Wire shape of a <see cref="MemberAgendaPattern"/> row.</summary>
public sealed record MemberAgendaPatternDto(
    Guid Id,
    Guid MemberId,
    DayOfWeek DayOfWeek,
    string Label,
    string? Location,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    AgendaTransportMode TransportMode,
    bool IsAway,
    string? Notes,
    bool IsActive);

/// <summary>Wire shape of a <see cref="MemberAgendaException"/> row.</summary>
public sealed record MemberAgendaExceptionDto(
    Guid Id,
    Guid MemberId,
    DateOnly Date,
    Guid? PatternId,
    bool IsCancelled,
    string? Label,
    string? Location,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    AgendaTransportMode? TransportMode,
    bool? IsAway,
    string? Notes);

/// <summary>One resolved agenda entry for a single (member, date) cell.</summary>
public sealed record ResolvedAgendaEntryDto(
    Guid MemberId,
    DateOnly Date,
    Guid? PatternId,
    Guid? ExceptionId,
    string Label,
    string? Location,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    AgendaTransportMode TransportMode,
    bool IsAway,
    string? Notes);

/// <summary>Bundle returned by <c>GET /api/member-agenda/overview</c>.</summary>
public sealed record MemberAgendaOverviewDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<MemberAgendaPatternDto> Patterns,
    IReadOnlyList<MemberAgendaExceptionDto> Exceptions,
    IReadOnlyList<ResolvedAgendaEntryDto> Resolved);
