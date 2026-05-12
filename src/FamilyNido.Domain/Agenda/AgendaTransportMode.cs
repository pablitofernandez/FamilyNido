namespace FamilyNido.Domain.Agenda;

/// <summary>
/// Means of transport used by a family member for an agenda entry. Drives the
/// icon shown on the dashboard widget and lays the groundwork for future
/// derivations like "the car is taken today". Independent of
/// <see cref="School.TransportMode"/> on purpose: school commute is narrow
/// (kids' day-to-day), agenda covers the whole family with broader options.
/// </summary>
public enum AgendaTransportMode
{
    /// <summary>Unknown / not specified.</summary>
    None = 0,

    /// <summary>Driving the family car.</summary>
    Car = 1,

    /// <summary>Bus.</summary>
    Bus = 2,

    /// <summary>Walking.</summary>
    Walk = 3,

    /// <summary>Train.</summary>
    Train = 4,

    /// <summary>Plane.</summary>
    Plane = 5,

    /// <summary>Anything else (taxi, bike, ride from a friend).</summary>
    Other = 6,
}
