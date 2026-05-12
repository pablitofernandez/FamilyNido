using System.Security.Claims;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Per-request context resolving the authenticated caller to their persisted
/// <see cref="User"/>, <see cref="FamilyMember"/> and <see cref="Family"/>.
/// Handles first-login bootstrapping (RF-USR-005, RF-AUTH-003) and stamps
/// the cookie with the internal <c>userId</c> claim used for fast lookups.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Steady-state lookup against <c>HttpContext.User</c>. Never mutates the
    /// database — use this from endpoints and hubs. Returns <c>null</c> when
    /// the caller is unauthenticated, the user row is gone, or the user has
    /// no <see cref="FamilyMember"/> linked yet (pre-onboarding).
    /// </summary>
    Task<CurrentUser?> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resolves only the <see cref="User"/> row, even when no
    /// <see cref="FamilyMember"/> is linked. Used by onboarding flows
    /// (accept-invitation) where the caller is authenticated but still orphan.
    /// Returns <c>null</c> when the caller is unauthenticated or the row is gone.
    /// </summary>
    Task<User?> GetUserAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Provisioning variant used inside the OIDC <c>OnTokenValidated</c> event
    /// where <c>HttpContext.User</c> is still anonymous. Bootstraps the first
    /// admin + family on a fresh database, creates an orphan <see cref="User"/>
    /// (plus its <see cref="UserCredential"/>) for new subjects on an
    /// already-initialized instance, or refreshes <c>LastLoginAt</c> on
    /// repeated logins.
    /// </summary>
    /// <returns>
    /// The resolved identity when an OIDC subject was present (linked or
    /// orphan); <c>null</c> when the principal carries no usable subject.
    /// </returns>
    Task<ResolvedIdentity?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

/// <summary>
/// Resolved per-request identity used by endpoints and SignalR hubs.
/// </summary>
/// <param name="User">Internal user row.</param>
/// <param name="Member">Linked family member.</param>
/// <param name="Family">Owning family.</param>
public sealed record CurrentUser(User User, FamilyMember Member, Family Family);

/// <summary>
/// Identity resolved during the OIDC callback. <see cref="Member"/> and
/// <see cref="Family"/> may be null while the user waits to be linked.
/// </summary>
/// <param name="User">Internal user row (always present once resolved).</param>
/// <param name="Member">Linked family member, or null when the user is orphan.</param>
/// <param name="Family">Owning family, or null when the user is orphan.</param>
public sealed record ResolvedIdentity(User User, FamilyMember? Member, Family? Family);
