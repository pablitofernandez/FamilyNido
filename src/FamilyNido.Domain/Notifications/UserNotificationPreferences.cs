using FamilyNido.Domain.Identity;

namespace FamilyNido.Domain.Notifications;

/// <summary>
/// Per-user toggles for outbound notifications. Defaults to "everything on" so
/// new users automatically get useful emails until they opt out. The row is
/// lazily upserted the first time the user opens the preferences screen.
/// </summary>
/// <remarks>
/// We keep the model intentionally narrow (one boolean per channel) so the
/// settings screen stays a checklist. Adding push or per-event granularity
/// later means new bool columns, not a new schema.
/// </remarks>
public sealed class UserNotificationPreferences
{
    /// <summary>Owning user. Doubles as the primary key — at most one row per user.</summary>
    public required Guid UserId { get; set; }

    /// <summary>Navigation to the owning <see cref="User"/>.</summary>
    public User? User { get; set; }

    /// <summary>Master switch. When false the dispatcher skips the user entirely.</summary>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>Receive the morning digest with today's tasks/events/birthdays.</summary>
    public bool DigestEnabled { get; set; } = true;

    /// <summary>Receive an email when assigned as the responsible of a task.</summary>
    public bool TaskAssignedEnabled { get; set; } = true;

    /// <summary>Receive an email when mentioned via <c>@</c> on the wall.</summary>
    public bool WallMentionEnabled { get; set; } = true;
}
