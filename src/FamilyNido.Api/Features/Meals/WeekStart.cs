namespace FamilyNido.Api.Features.Meals;

/// <summary>
/// Helpers around the "week starts on Monday" convention used by the meal
/// planner. Centralized so the snap logic is identical in every slice.
/// </summary>
public static class WeekStart
{
    /// <summary>
    /// Snaps any date to the Monday of its ISO week (or returns the same
    /// date if it already is a Monday).
    /// </summary>
    public static DateOnly SnapToMonday(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Mon=0, Sun=6
        return date.AddDays(-diff);
    }
}
