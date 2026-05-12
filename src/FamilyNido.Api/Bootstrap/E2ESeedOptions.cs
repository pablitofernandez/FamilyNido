namespace FamilyNido.Api.Bootstrap;

/// <summary>
/// Strongly-typed binding for the <c>Seed:E2E</c> configuration section.
/// Only honored when <see cref="IHostEnvironment.EnvironmentName"/> is
/// <c>Testing</c>; production never reads this section.
/// </summary>
public sealed class E2ESeedOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Seed:E2E";

    /// <summary>Master switch. The seeder no-ops unless this is true.</summary>
    public bool Enabled { get; init; }

    /// <summary>Name of the test family. Used as the idempotency key.</summary>
    public string FamilyName { get; init; } = "FamilyNido E2E";

    /// <summary>IANA time zone for the seeded family.</summary>
    public string TimeZone { get; init; } = "Europe/Madrid";

    /// <summary>Email of admin tester A. Stable across runs.</summary>
    public string UserAEmail { get; init; } = "e2e-a@FamilyNido.test";

    /// <summary>Local-credential password for tester A. Required when <see cref="Enabled"/> is true.</summary>
    public string UserAPassword { get; init; } = string.Empty;

    /// <summary>Display name for tester A.</summary>
    public string UserADisplayName { get; init; } = "Tester A";

    /// <summary>Hex color for tester A's avatar.</summary>
    public string UserAColorHex { get; init; } = "#3B82F6";

    /// <summary>Email of adult tester B.</summary>
    public string UserBEmail { get; init; } = "e2e-b@FamilyNido.test";

    /// <summary>Local-credential password for tester B. Required when <see cref="Enabled"/> is true.</summary>
    public string UserBPassword { get; init; } = string.Empty;

    /// <summary>Display name for tester B.</summary>
    public string UserBDisplayName { get; init; } = "Tester B";

    /// <summary>Hex color for tester B's avatar.</summary>
    public string UserBColorHex { get; init; } = "#EF4444";
}
