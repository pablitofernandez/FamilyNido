using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Calendar;

/// <summary>
/// Mirrored Google Calendar event. Rows are owned by a <see cref="LinkedCalendar"/>
/// and refreshed by the periodic sync engine via incremental sync tokens. The app is
/// strictly read-only against this table; users edit events in Google Calendar and
/// the next sync brings the changes here.
/// </summary>
public sealed class CalendarEvent : AuditableEntity
{
    /// <summary>Family this event ultimately belongs to (denormalized for fast queries).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Calendar of origin within the linked Google account.</summary>
    public required Guid LinkedCalendarId { get; set; }

    /// <summary>Navigation to the owning <see cref="LinkedCalendar"/>.</summary>
    public LinkedCalendar? LinkedCalendar { get; set; }

    /// <summary>Google's event id. Unique within a calendar.</summary>
    public required string ExternalEventId { get; set; }

    /// <summary>
    /// iCalendar UID — stable across recurrence instances and across calendars when an
    /// event is shared. Useful for grouping and de-duplication in dashboards.
    /// </summary>
    public string? IcalUid { get; set; }

    /// <summary>Event title ("summary" in Google's API).</summary>
    public required string Title { get; set; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional location string.</summary>
    public string? Location { get; set; }

    /// <summary>Start instant in UTC. For all-day events, the start is midnight in <see cref="OriginalTimeZone"/>.</summary>
    public required DateTimeOffset StartAt { get; set; }

    /// <summary>End instant in UTC. Google's convention: for all-day events, end is exclusive.</summary>
    public required DateTimeOffset EndAt { get; set; }

    /// <summary>True when the event has no specific time-of-day (a "date" in Google's API instead of "dateTime").</summary>
    public bool IsAllDay { get; set; }

    /// <summary>Original IANA timezone reported by Google (<c>"Europe/Madrid"</c> by default for our users).</summary>
    public string? OriginalTimeZone { get; set; }

    /// <summary>Public link back to the event in Google Calendar (<c>htmlLink</c>).</summary>
    public string? HtmlLink { get; set; }

    /// <summary>
    /// Locally-managed N:M of family members the event concerns ("a quién va") —
    /// independent from the calendar-level <see cref="LinkedCalendar.FamilyMemberId"/>
    /// default. Persisted in FamilyNido and survives Google re-syncs because the sync
    /// engine upserts in-place on the same <see cref="ExternalEventId"/>.
    /// </summary>
    public ICollection<FamilyMember> RelatedMembers { get; set; } = [];
}
