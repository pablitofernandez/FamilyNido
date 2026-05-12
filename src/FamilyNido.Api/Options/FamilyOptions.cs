namespace FamilyNido.Api.Options;

/// <summary>Strongly-typed binding for the <c>Family</c> configuration section.</summary>
public sealed class FamilyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Family";

    /// <summary>Name given to the family when auto-bootstrapped on first login.</summary>
    public string DefaultName { get; init; } = "Mi familia";

    /// <summary>IANA time zone assigned to a newly bootstrapped family.</summary>
    public string DefaultTimeZone { get; init; } = "Europe/Madrid";

    /// <summary>BCP-47 locale assigned to a newly bootstrapped family.</summary>
    public string DefaultLocale { get; init; } = "es-ES";

    /// <summary>Apply pending EF Core migrations automatically at startup.</summary>
    public bool AutoMigrate { get; init; } = true;

    /// <summary>
    /// When true and the database is empty, the very first authenticated user
    /// becomes the admin of a freshly created family. See RF-AUTH-003.
    /// </summary>
    public bool BootstrapFirstUserAsAdmin { get; init; } = true;
}
