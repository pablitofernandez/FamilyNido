namespace FamilyNido.Domain.HouseholdTasks;

/// <summary>
/// ISO-8601 ordered flag set for <see cref="RecurrenceMode.Weekly"/> tasks. Uses a bitmask
/// (persisted as <c>smallint</c>) so a weekly pattern like "Monday + Thursday" is a single
/// value. Monday is the first day of the week (coherent with ES and most of the EU).
/// </summary>
[Flags]
public enum DayOfWeekMask : short
{
    /// <summary>No days selected. Equivalent to "never occurs" when <c>Recurrence == Weekly</c>.</summary>
    None = 0,

    /// <summary>Lunes.</summary>
    Monday = 1,

    /// <summary>Martes.</summary>
    Tuesday = 1 << 1,

    /// <summary>Miércoles.</summary>
    Wednesday = 1 << 2,

    /// <summary>Jueves.</summary>
    Thursday = 1 << 3,

    /// <summary>Viernes.</summary>
    Friday = 1 << 4,

    /// <summary>Sábado.</summary>
    Saturday = 1 << 5,

    /// <summary>Domingo.</summary>
    Sunday = 1 << 6,

    /// <summary>Lunes a viernes.</summary>
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,

    /// <summary>Sábado y domingo.</summary>
    Weekend = Saturday | Sunday,

    /// <summary>Todos los días de la semana.</summary>
    All = Weekdays | Weekend,
}
