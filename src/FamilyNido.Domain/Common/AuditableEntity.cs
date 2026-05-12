namespace FamilyNido.Domain.Common;

/// <summary>
/// Base for persisted entities that carry identity + auditing metadata.
/// Audit columns are populated automatically by the DbContext's SaveChanges override.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>Primary key. UUIDv7 for time-ordered, index-friendly ids.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>UTC instant of row creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>OIDC subject (or "system") of the actor that created the row.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>UTC instant of the last mutation. Null until first update.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>OIDC subject (or "system") of the actor that last updated the row.</summary>
    public string? UpdatedBy { get; set; }
}
