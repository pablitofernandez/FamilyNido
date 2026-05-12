namespace FamilyNido.Api.Features.Dashboard;

/// <summary>
/// Catalogue of widget identifiers known to the dashboard. Anything outside
/// this list is rejected at the application layer when the user submits a
/// new layout — the frontend mirrors the IDs verbatim.
/// </summary>
/// <remarks>
/// Adding a widget is a three-step dance: extend this list (and document the
/// label), include it in <see cref="DefaultOrder"/>, and render it in the
/// dashboard component. New widgets default to visible at the bottom for
/// existing users.
/// </remarks>
public static class DashboardWidgets
{
    /// <summary>Open-Meteo current-weather card.</summary>
    public const string Weather = "weather";

    /// <summary>Today's bus / extras / drop-off + pick-up summary.</summary>
    public const string School = "school";

    /// <summary>Pending household tasks for today.</summary>
    public const string Tasks = "tasks";

    /// <summary>Top of the upcoming Google Calendar events list.</summary>
    public const string Calendar = "calendar";

    /// <summary>Today's and tomorrow's planned meals.</summary>
    public const string Meals = "meals";

    /// <summary>Pinned wall messages.</summary>
    public const string Wall = "wall";

    /// <summary>Birthdays in the next 30 days.</summary>
    public const string Birthdays = "birthdays";

    /// <summary>Today's agenda — who is away from home and what for.</summary>
    public const string Agenda = "agenda";

    /// <summary>Family scoreboard — points earned this week from completed tasks.</summary>
    public const string Scores = "scores";

    /// <summary>Default order applied when a user has no row yet.</summary>
    public static IReadOnlyList<string> DefaultOrder => new[]
    {
        Weather, School, Agenda, Tasks, Calendar, Meals, Wall, Scores, Birthdays,
    };

    /// <summary>True when <paramref name="id"/> is a known widget id.</summary>
    public static bool IsKnown(string id) => DefaultOrder.Contains(id);
}
