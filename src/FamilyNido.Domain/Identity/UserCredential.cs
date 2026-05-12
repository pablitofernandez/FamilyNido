using FamilyNido.Domain.Common;

namespace FamilyNido.Domain.Identity;

/// <summary>
/// A single way to authenticate as a given <see cref="User"/>. A user may have
/// many: each row binds one provider (OIDC subject or local password hash) to
/// the same internal <see cref="UserId"/>.
/// </summary>
/// <remarks>
/// Coherence is enforced both at the application layer and via a database
/// CHECK constraint:
/// <list type="bullet">
///   <item><see cref="IdentityProvider.Oidc"/> rows have <see cref="ProviderKey"/> set and <see cref="PasswordHash"/> null.</item>
///   <item><see cref="IdentityProvider.Local"/> rows have <see cref="PasswordHash"/> set and <see cref="ProviderKey"/> null.</item>
/// </list>
/// </remarks>
public sealed class UserCredential : AuditableEntity
{
    /// <summary>Owning user.</summary>
    public required Guid UserId { get; set; }

    /// <summary>Navigation back to the owner.</summary>
    public User? User { get; set; }

    /// <summary>How the caller proves their identity.</summary>
    public required IdentityProvider Provider { get; set; }

    /// <summary>OIDC <c>sub</c> claim (issued by the provider). Null for local credentials.</summary>
    public string? ProviderKey { get; set; }

    /// <summary>PBKDF2 password hash (ASP.NET Core <c>PasswordHasher</c> v3 format). Null for OIDC.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>UTC timestamp of the most recent successful authentication using this credential.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }
}
