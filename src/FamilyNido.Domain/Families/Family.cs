using FamilyNido.Domain.Common;

namespace FamilyNido.Domain.Families;

/// <summary>
/// Aggregate root representing the family unit. Every tenant-scoped entity hangs
/// off a <see cref="Family"/>; FamilyNido is single-family per deployment but the
/// model keeps this explicit so future multi-tenant variants stay feasible.
/// </summary>
public sealed class Family : AuditableEntity
{
    /// <summary>Display name of the family, shown in the header (e.g. "Familia Demo").</summary>
    public required string Name { get; set; }

    /// <summary>IANA time zone (e.g. "Europe/Madrid") used to render dates and trigger reminders.</summary>
    public required string TimeZone { get; set; }

    /// <summary>BCP-47 locale used for formatting (e.g. "es-ES"). Individual users may override.</summary>
    public string Locale { get; set; } = "es-ES";

    /// <summary>Geographic latitude (decimal degrees). Drives the dashboard weather widget when set.</summary>
    public double? Latitude { get; set; }

    /// <summary>Geographic longitude (decimal degrees). Always paired with <see cref="Latitude"/>.</summary>
    public double? Longitude { get; set; }

    /// <summary>Human-readable label for the location (e.g. "Bilbao"). Displayed under the weather widget.</summary>
    public string? LocationLabel { get; set; }

    /// <summary>Members of this family. Navigation property loaded on demand.</summary>
    public ICollection<FamilyMember> Members { get; set; } = [];
}
