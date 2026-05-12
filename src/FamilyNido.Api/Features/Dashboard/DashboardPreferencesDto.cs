namespace FamilyNido.Api.Features.Dashboard;

/// <summary>One widget entry in the user's dashboard layout.</summary>
/// <param name="Id">Stable widget identifier (see <see cref="DashboardWidgets"/>).</param>
/// <param name="Visible">True when the widget is rendered on the dashboard.</param>
public sealed record DashboardWidgetDto(string Id, bool Visible);

/// <summary>
/// Wire shape returned by <c>GET /api/dashboard/preferences</c>: the ordered
/// list of widget entries. Always includes every known widget so the frontend
/// renders a complete settings screen even when the user has never touched it.
/// </summary>
public sealed record DashboardPreferencesDto(IReadOnlyList<DashboardWidgetDto> Widgets);
