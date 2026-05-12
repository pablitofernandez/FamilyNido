using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Calendar;

/// <summary>
/// A specific Google Calendar imported under a <see cref="GoogleAccount"/>. Each
/// row represents one calendar visible to that account (primary, shared, holidays…).
/// Only those flagged with <see cref="IsImported"/> contribute events to the family
/// view; the rest are kept as discoverable metadata so the user can flip them on later.
/// </summary>
public sealed class LinkedCalendar : AuditableEntity
{
    /// <summary>Owning Google account.</summary>
    public required Guid GoogleAccountId { get; set; }

    /// <summary>Navigation to the owning <see cref="GoogleAccount"/>.</summary>
    public GoogleAccount? GoogleAccount { get; set; }

    /// <summary>
    /// Google's calendar identifier (<c>"primary"</c>, an email, or
    /// <c>...@group.calendar.google.com</c>). Unique within an account.
    /// </summary>
    public required string ExternalCalendarId { get; set; }

    /// <summary>Human-readable name reported by Google.</summary>
    public required string Summary { get; set; }

    /// <summary>Optional description from Google.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Hex color (<c>#RRGGBB</c>) reported by Google. Used as fallback when the
    /// calendar is not assigned to a family member.
    /// </summary>
    public string? ColorHex { get; set; }

    /// <summary>
    /// True when events from this calendar are mirrored into FamilyNido. Toggling off
    /// purges the cached events on the next sync.
    /// </summary>
    public bool IsImported { get; set; }

    /// <summary>
    /// Optional family member to which this calendar is associated. Drives the color
    /// of the events in the UI. Null means "use the Google color".
    /// </summary>
    public Guid? FamilyMemberId { get; set; }

    /// <summary>Navigation to the assigned <see cref="FamilyMember"/>.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>
    /// Latest <c>nextSyncToken</c> returned by Google. Null forces a full sync on the
    /// next run (initial import, after a 410 Gone, or after toggling import on).
    /// </summary>
    public string? SyncToken { get; set; }

    /// <summary>UTC instant of the last successful sync. Null until first sync.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>Cached events mirrored from Google; cascade-deleted with the calendar.</summary>
    public ICollection<CalendarEvent> Events { get; set; } = [];
}
