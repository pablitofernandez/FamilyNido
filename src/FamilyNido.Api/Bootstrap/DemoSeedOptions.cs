namespace FamilyNido.Api.Bootstrap;

/// <summary>
/// Strongly-typed binding for the <c>Seed:Demo</c> configuration section.
/// Drives <see cref="DemoDataSeeder"/>, which only registers in the
/// <c>Development</c> environment and no-ops unless <see cref="Enabled"/> is
/// true. Used to drop a curated, screenshot-friendly scenario into an empty
/// database so the README assets can be captured without touching real data.
/// </summary>
public sealed class DemoSeedOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Seed:Demo";

    /// <summary>Master switch. The seeder no-ops unless this is true.</summary>
    public bool Enabled { get; init; }

    /// <summary>Display name of the demo family. Idempotency key — re-running with
    /// the same name is a no-op once the family exists.</summary>
    public string FamilyName { get; init; } = "Smith Family";

    /// <summary>IANA time zone for the demo family. The seeder anchors all dates
    /// (tasks, meals, completions) to "today" in this zone so screenshots taken
    /// in different time zones still look coherent.</summary>
    public string TimeZone { get; init; } = "Europe/Madrid";

    /// <summary>BCP-47 locale used for date/number formatting in the UI. The
    /// per-user <c>PreferredLanguage</c> drives the actual UI language; pick
    /// <c>en-US</c> here if your screenshots target an English README.</summary>
    public string Locale { get; init; } = "en-US";

    /// <summary>Preferred language each demo adult ends up with. Same hint as
    /// <see cref="Locale"/> — overrides at the user level.</summary>
    public string PreferredLanguage { get; init; } = "en-US";

    /// <summary>Human-readable label rendered under the weather widget.</summary>
    public string LocationLabel { get; init; } = "Bilbao";

    /// <summary>Geographic latitude in decimal degrees. Drives the weather widget.</summary>
    public double Latitude { get; init; } = 43.2630;

    /// <summary>Geographic longitude in decimal degrees.</summary>
    public double Longitude { get; init; } = -2.9350;

    /// <summary>Email used for the primary adult (admin). Required to be able to
    /// log in and capture screenshots; the password lives in
    /// <see cref="AdminPassword"/>.</summary>
    public string AdminEmail { get; init; } = "dan@familynido.demo";

    /// <summary>Display name of the primary adult.</summary>
    public string AdminDisplayName { get; init; } = "Dan";

    /// <summary>Local-credential password for the admin. Required when
    /// <see cref="Enabled"/> is true.</summary>
    public string AdminPassword { get; init; } = string.Empty;

    /// <summary>Email of the second adult.</summary>
    public string PartnerEmail { get; init; } = "eve@familynido.demo";

    /// <summary>Display name of the second adult.</summary>
    public string PartnerDisplayName { get; init; } = "Eve";

    /// <summary>Local-credential password for the second adult. Optional — if
    /// empty, the partner is created without local credentials (you can still
    /// browse as the admin).</summary>
    public string PartnerPassword { get; init; } = string.Empty;
}
