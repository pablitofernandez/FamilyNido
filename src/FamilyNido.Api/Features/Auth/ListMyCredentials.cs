using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Slice: the authenticated user inspects their own credentials. Used by the
/// "Mi cuenta" screen to show which login methods are active and to drive
/// the "establecer / cambiar / eliminar" buttons.
/// </summary>
public static class ListMyCredentials
{
    /// <summary>Query — no inputs.</summary>
    public sealed record Query : IRequest<Result<IReadOnlyList<CredentialDto>>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<CredentialDto>>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext)
        {
            _db = db;
            _userContext = userContext;
        }

        /// <inheritdoc />
        public async Task<Result<IReadOnlyList<CredentialDto>>> HandleAsync(Query request, CancellationToken ct)
        {
            var user = await _userContext.GetUserAsync(ct);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Caller is not authenticated.");
            }

            var rows = await _db.UserCredentials
                .AsNoTracking()
                .Where(c => c.UserId == user.Id)
                .OrderBy(c => c.Provider)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync(ct);

            IReadOnlyList<CredentialDto> dto = [.. rows.Select(c => new CredentialDto(
                Id: c.Id,
                Provider: c.Provider,
                CreatedAt: c.CreatedAt,
                LastUsedAt: c.LastUsedAt,
                ProviderKeyHint: c.Provider == IdentityProvider.Oidc ? Hint(c.ProviderKey) : null))];

            return Result<IReadOnlyList<CredentialDto>>.Success(dto);
        }

        private static string? Hint(string? key)
            => string.IsNullOrEmpty(key) ? null : key.Length <= 6 ? key : $"…{key[^6..]}";
    }
}

/// <summary>Read-model returned by <c>GET /api/auth/credentials</c>.</summary>
/// <param name="Id">Credential id.</param>
/// <param name="Provider">Provider type.</param>
/// <param name="CreatedAt">UTC instant the credential was added.</param>
/// <param name="LastUsedAt">UTC instant of last successful login through this credential.</param>
/// <param name="ProviderKeyHint">Last 6 chars of the OIDC subject (for UX disambiguation). Null for Local.</param>
public sealed record CredentialDto(
    Guid Id,
    IdentityProvider Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    string? ProviderKeyHint);
