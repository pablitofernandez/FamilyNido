namespace FamilyNido.Domain.HouseholdTasks;

/// <summary>
/// Recurrence pattern for a <see cref="HouseholdTask"/>. Deliberately limited to the
/// handful of patterns that cover household use cases ("fregar los lunes", "limpiar el
/// primer día del mes"); complex rules (e.g. "el tercer viernes") are out of scope —
/// the module owns its own motor, independent of the calendar's iCalendar RRULE.
/// </summary>
public enum RecurrenceMode
{
    /// <summary>Single-occurrence task (with optional due date).</summary>
    None = 0,

    /// <summary>Repeats every day within the active window.</summary>
    Daily = 1,

    /// <summary>Repeats on selected days of the week — see <see cref="HouseholdTask.WeeklyDays"/>.</summary>
    Weekly = 2,

    /// <summary>Repeats on a specific day of each month — see <see cref="HouseholdTask.MonthlyDay"/>.</summary>
    Monthly = 3,
}
