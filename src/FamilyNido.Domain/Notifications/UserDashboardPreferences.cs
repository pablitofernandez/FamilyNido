using FamilyNido.Domain.Identity;

namespace FamilyNido.Domain.Notifications;

/// <summary>
/// Per-user customisation of the dashboard layout: which widgets are visible
/// and in what order. Persisted as a single JSON column so adding new widget
/// kinds in the future doesn't require a schema change.
/// </summary>
/// <remarks>
/// The row is created lazily on first save. When absent, the API surfaces the
/// default order with every widget visible — that's why all properties on the
/// DTO returned by <c>GET /api/dashboard/preferences</c> are populated even
/// for users that never opened the settings screen.
/// </remarks>
public sealed class UserDashboardPreferences
{
    /// <summary>Owning user. Doubles as the primary key — at most one row per user.</summary>
    public required Guid UserId { get; set; }

    /// <summary>Navigation to the owning <see cref="User"/>.</summary>
    public User? User { get; set; }

    /// <summary>JSON string with the ordered list of <c>{id,visible}</c> objects.</summary>
    public string WidgetsJson { get; set; } = string.Empty;
}
