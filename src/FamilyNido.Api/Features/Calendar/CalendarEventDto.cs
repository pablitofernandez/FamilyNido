using FamilyNido.Domain.Calendar;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>Wire-level view of a mirrored Google Calendar event.</summary>
/// <param name="Id">FamilyNido id of the event row.</param>
/// <param name="LinkedCalendarId">Calendar of origin within the linked account.</param>
/// <param name="FamilyMemberId">Family member associated to the source calendar (if any) — drives the color in the UI.</param>
/// <param name="Title">Event title.</param>
/// <param name="Description">Optional longer description.</param>
/// <param name="Location">Optional location string.</param>
/// <param name="StartAt">Start instant in UTC.</param>
/// <param name="EndAt">End instant in UTC.</param>
/// <param name="IsAllDay">True when the event has no specific time-of-day.</param>
/// <param name="OriginalTimeZone">Original IANA timezone reported by Google.</param>
/// <param name="HtmlLink">Public link back to the event in Google Calendar.</param>
/// <param name="RelatedMemberIds">Per-event N:M of family members tagged locally on this event (independent of the calendar default).</param>
public sealed record CalendarEventDto(
    Guid Id,
    Guid LinkedCalendarId,
    Guid? FamilyMemberId,
    string Title,
    string? Description,
    string? Location,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsAllDay,
    string? OriginalTimeZone,
    string? HtmlLink,
    IReadOnlyList<Guid> RelatedMemberIds)
{
    /// <summary>Builds a DTO from the persisted entity (assumes <see cref="CalendarEvent.LinkedCalendar"/> and RelatedMembers are loaded).</summary>
    public static CalendarEventDto From(CalendarEvent ev)
        => new(
            ev.Id,
            ev.LinkedCalendarId,
            ev.LinkedCalendar?.FamilyMemberId,
            ev.Title,
            ev.Description,
            ev.Location,
            ev.StartAt,
            ev.EndAt,
            ev.IsAllDay,
            ev.OriginalTimeZone,
            ev.HtmlLink,
            [.. ev.RelatedMembers.Select(m => m.Id)]);
}
