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
/// <param name="StartDate">
/// For all-day events, the inclusive start date (<c>YYYY-MM-DD</c>) as it
/// appears in Google Calendar — interpreted in <see cref="OriginalTimeZone"/>
/// so it does not shift with the viewer's timezone. Null for timed events
/// (the UI should derive the day from <see cref="StartAt"/> in those).
/// </param>
/// <param name="EndDate">For all-day events, the exclusive end date (Google convention). Null for timed events.</param>
/// <param name="OriginalTimeZone">IANA timezone used to interpret <see cref="StartAt"/> / <see cref="EndAt"/>.</param>
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
    string? StartDate,
    string? EndDate,
    string? OriginalTimeZone,
    string? HtmlLink,
    IReadOnlyList<Guid> RelatedMemberIds)
{
    /// <summary>Builds a DTO from the persisted entity (assumes <see cref="CalendarEvent.LinkedCalendar"/> and RelatedMembers are loaded).</summary>
    public static CalendarEventDto From(CalendarEvent ev)
    {
        string? startDate = null;
        string? endDate = null;
        if (ev.IsAllDay)
        {
            // Recover the calendar date by converting the stored UTC instant
            // back to the event's original timezone. The synchronizer pins
            // OriginalTimeZone to the family TZ for all-day events (issue #13),
            // so absent that the UTC fallback is "least wrong" rather than
            // "right" — we'd be back to the pre-fix behaviour for legacy rows.
            var tz = CalendarTimeZones.TryFind(ev.OriginalTimeZone) ?? TimeZoneInfo.Utc;
            startDate = CalendarAllDayResolver.FormatLocalDate(ev.StartAt, tz);
            endDate = CalendarAllDayResolver.FormatLocalDate(ev.EndAt, tz);
        }

        return new(
            ev.Id,
            ev.LinkedCalendarId,
            ev.LinkedCalendar?.FamilyMemberId,
            ev.Title,
            ev.Description,
            ev.Location,
            ev.StartAt,
            ev.EndAt,
            ev.IsAllDay,
            startDate,
            endDate,
            ev.OriginalTimeZone,
            ev.HtmlLink,
            [.. ev.RelatedMembers.Select(m => m.Id)]);
    }
}
