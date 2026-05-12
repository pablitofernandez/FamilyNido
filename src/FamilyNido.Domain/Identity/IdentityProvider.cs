namespace FamilyNido.Domain.Identity;

/// <summary>
/// How a <see cref="UserCredential"/> proves the caller is a given <see cref="User"/>.
/// </summary>
/// <remarks>
/// Each user can carry several credentials simultaneously: e.g. one
/// <see cref="Oidc"/> credential bound to PocketID and one <see cref="Local"/>
/// credential with a stored password hash. The user picks how to log in each
/// time; both routes resolve to the same <see cref="User"/> id.
/// </remarks>
public enum IdentityProvider
{
    /// <summary>OpenID Connect identity (e.g. PocketID). Stores the OIDC <c>sub</c>.</summary>
    Oidc = 0,

    /// <summary>Local email + password identity. Stores a PBKDF2 password hash.</summary>
    Local = 1,
}
