using System.Data;
using System.Security.Claims;
using FamilyNido.Api.Options;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Default implementation of <see cref="ICurrentUserContext"/>. Caches the
/// lookup per request (it is registered scoped) so repeated calls within a
/// single HTTP request do not re-hit the database.
/// </summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    /// <summary>Custom claim type holding the internal <see cref="User"/> id (Guid string).</summary>
    public const string UserIdClaimType = "userId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly FamilyOptions _familyOptions;

    private CurrentUser? _cached;
    private bool _resolved;

    /// <summary>Primary constructor.</summary>
    public CurrentUserContext(
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext db,
        TimeProvider timeProvider,
        IOptions<FamilyOptions> familyOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
        _timeProvider = timeProvider;
        _familyOptions = familyOptions.Value;
    }

    /// <inheritdoc />
    public async Task<User?> GetUserAsync(CancellationToken cancellationToken)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return await ResolvePrincipalUserAsync(principal, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CurrentUser?> GetAsync(CancellationToken cancellationToken)
    {
        if (_resolved)
        {
            return _cached;
        }

        _resolved = true;

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return _cached = null;
        }

        var user = await ResolvePrincipalUserAsync(principal, cancellationToken);
        // Orphan users (no FamilyMember linked yet) get the same null treatment as
        // unauthenticated callers from the handlers' perspective. The /api/auth/me
        // slice translates this into a 403 auth.not_linked so the front can route
        // them to the "ask the admin for an invitation" screen.
        if (user?.FamilyMember?.Family is null)
        {
            return _cached = null;
        }

        return _cached = new CurrentUser(user, user.FamilyMember, user.FamilyMember.Family);
    }

    /// <inheritdoc />
    public async Task<ResolvedIdentity?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var subject = ExtractOidcSubject(principal);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
                    ?? principal.FindFirst("email")?.Value
                    ?? "";
        var displayName = principal.FindFirst("name")?.Value
                          ?? principal.FindFirst(ClaimTypes.Name)?.Value
                          ?? (email.Contains('@', StringComparison.Ordinal)
                              ? email.Split('@', 2)[0]
                              : subject);

        var credential = await _db.UserCredentials
            .Include(c => c.User)
                .ThenInclude(u => u!.FamilyMember)
                    .ThenInclude(m => m!.Family)
            .FirstOrDefaultAsync(
                c => c.Provider == IdentityProvider.Oidc && c.ProviderKey == subject,
                cancellationToken);

        if (credential is { User: not null })
        {
            var existing = credential.User;
            existing.LastLoginAt = _timeProvider.GetUtcNow();
            credential.LastUsedAt = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);

            return new ResolvedIdentity(existing, existing.FamilyMember, existing.FamilyMember?.Family);
        }

        // First-ever login path. The "is the DB empty?" check followed by the
        // family + admin insert needs to be atomic — without serialization,
        // two concurrent OIDC callbacks could both see an empty Users table
        // and each spawn a separate family marked admin. A Serializable
        // transaction makes Postgres reject one of the two with a 40001
        // serialization_failure; that request reads the now-non-empty state
        // on retry and falls into the orphan branch as intended.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var alreadyInitialized = await _db.Users.AnyAsync(cancellationToken);
        if (!alreadyInitialized && _familyOptions.BootstrapFirstUserAsAdmin)
        {
            var identity = await BootstrapFirstAdminAsync(subject, email, displayName, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return identity;
        }

        // New OIDC subject on an already-initialized instance. Persist an orphan
        // User + its credential so an admin can list and invite them later, and
        // so the cookie can carry a stable `userId` claim. The caller (GetMe)
        // still translates "no FamilyMember" into 403 auth.not_linked.
        var orphan = await CreateOrphanUserAsync(subject, email, displayName, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return orphan;
    }

    private async Task<ResolvedIdentity> BootstrapFirstAdminAsync(
        string subject,
        string email,
        string displayName,
        CancellationToken ct)
    {
        var newFamily = new Family
        {
            Name = _familyOptions.DefaultName,
            TimeZone = _familyOptions.DefaultTimeZone,
            Locale = _familyOptions.DefaultLocale,
        };

        var newUser = new User
        {
            Email = email,
            DisplayName = displayName,
            Role = FamilyRole.Admin,
            LastLoginAt = _timeProvider.GetUtcNow(),
        };

        var newCredential = new UserCredential
        {
            UserId = newUser.Id,
            User = newUser,
            Provider = IdentityProvider.Oidc,
            ProviderKey = subject,
            LastUsedAt = _timeProvider.GetUtcNow(),
        };

        var newMember = new FamilyMember
        {
            FamilyId = newFamily.Id,
            Family = newFamily,
            DisplayName = displayName,
            MemberType = MemberType.Adult,
            ColorHex = "#C96442",
            ContactEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            User = newUser,
        };

        _db.Families.Add(newFamily);
        _db.Users.Add(newUser);
        _db.UserCredentials.Add(newCredential);
        _db.FamilyMembers.Add(newMember);
        await _db.SaveChangesAsync(ct);

        return new ResolvedIdentity(newUser, newMember, newFamily);
    }

    private async Task<ResolvedIdentity> CreateOrphanUserAsync(
        string subject,
        string email,
        string displayName,
        CancellationToken ct)
    {
        var newUser = new User
        {
            Email = email,
            DisplayName = displayName,
            Role = FamilyRole.Guest,
            LastLoginAt = _timeProvider.GetUtcNow(),
        };

        var newCredential = new UserCredential
        {
            UserId = newUser.Id,
            User = newUser,
            Provider = IdentityProvider.Oidc,
            ProviderKey = subject,
            LastUsedAt = _timeProvider.GetUtcNow(),
        };

        _db.Users.Add(newUser);
        _db.UserCredentials.Add(newCredential);
        await _db.SaveChangesAsync(ct);

        return new ResolvedIdentity(newUser, null, null);
    }

    /// <summary>
    /// Resolves the persisted <see cref="User"/> for an authenticated principal.
    /// Prefers the fast <c>userId</c> claim baked into the cookie at login time;
    /// falls back to the OIDC <c>sub</c> claim via <see cref="UserCredential"/>
    /// for legacy sessions issued before this claim was added.
    /// </summary>
    private async Task<User?> ResolvePrincipalUserAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var userIdClaim = principal.FindFirst(UserIdClaimType)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return await _db.Users
                .AsNoTracking()
                .Include(u => u.FamilyMember)
                    .ThenInclude(m => m!.Family)
                .FirstOrDefaultAsync(u => u.Id == userId, ct);
        }

        var subject = ExtractOidcSubject(principal);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        return await _db.Users
            .AsNoTracking()
            .Include(u => u.FamilyMember)
                .ThenInclude(m => m!.Family)
            .FirstOrDefaultAsync(
                u => u.Credentials.Any(c => c.Provider == IdentityProvider.Oidc && c.ProviderKey == subject),
                ct);
    }

    private static string? ExtractOidcSubject(ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? principal.FindFirst("sub")?.Value;
}
