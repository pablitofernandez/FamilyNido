namespace FamilyNido.Api.Options;

/// <summary>Strongly-typed binding for the <c>Cors</c> configuration section.</summary>
public sealed class CorsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Allowed origins for the Angular SPA during development.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}
