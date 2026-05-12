using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Health;

/// <summary>
/// Lightweight medical "card" for a single <see cref="FamilyMember"/>: blood
/// group, free-text allergies, chronic conditions and miscellaneous notes.
/// One row per member at most — the profile is created lazily the first time
/// somebody saves it.
/// </summary>
/// <remarks>
/// FamilyNido is not a clinical record system: this profile is a memory aid for
/// the household (e.g. handing the right info to a babysitter or to the GP).
/// Sensitive enough to keep the module gated to <c>Admin</c>/<c>Adult</c>
/// roles, but never validated against any medical taxonomy.
/// </remarks>
public sealed class HealthProfile : AuditableEntity
{
    /// <summary>Member this profile belongs to. PK of the row through unique index.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the owning <see cref="FamilyMember"/>.</summary>
    public FamilyMember? FamilyMember { get; set; }

    /// <summary>Blood group as a short string ("A+", "O-", "AB+", …). Null when unknown.</summary>
    public string? BloodType { get; set; }

    /// <summary>Free-text list of allergies. Multi-line is allowed; the UI splits on newlines.</summary>
    public string? Allergies { get; set; }

    /// <summary>Free-text list of chronic conditions (asma, diabetes, …).</summary>
    public string? ChronicConditions { get; set; }

    /// <summary>Free-text general notes shown at the bottom of the card.</summary>
    public string? Notes { get; set; }
}
