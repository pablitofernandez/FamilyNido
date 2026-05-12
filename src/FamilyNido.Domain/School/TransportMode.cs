namespace FamilyNido.Domain.School;

/// <summary>
/// How a child gets to and from their school / daycare. Drives which slots of
/// <see cref="SchoolDaySchedule"/> are meaningful and what icon the dashboard
/// surfaces — it is not the source of truth for who-does-what (that lives in
/// the schedule rows themselves).
/// </summary>
public enum TransportMode
{
    /// <summary>No managed transport — older kids that walk by themselves.</summary>
    None = 0,

    /// <summary>School bus: the child rides one way and a caretaker picks them up.</summary>
    Bus = 1,

    /// <summary>Walked to and from the centre by a caretaker.</summary>
    Walk = 2,

    /// <summary>Driven to and from the centre by a caretaker.</summary>
    Car = 3,
}
