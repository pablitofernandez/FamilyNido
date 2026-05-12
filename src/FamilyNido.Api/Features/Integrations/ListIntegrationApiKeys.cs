using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Integrations;

/// <summary>Slice for <c>GET /api/integrations/api-keys</c> — admin lists tokens of their family.</summary>
public static class ListIntegrationApiKeys
{
    /// <summary>Empty query — caller is the implicit subject (and must be admin).</summary>
    public sealed record Query : IRequest<Result<IReadOnlyList<IntegrationApiKeyDto>>>;

    /// <summary>Reads the family's tokens, newest first.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<IntegrationApiKeyDto>>>
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
        public async Task<Result<IReadOnlyList<IntegrationApiKeyDto>>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var rows = await _db.IntegrationApiKeys
                .AsNoTracking()
                .Where(k => k.FamilyId == current.Family.Id)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new IntegrationApiKeyDto(
                    k.Id,
                    k.Name,
                    k.Prefix,
                    k.CreatedAt,
                    k.LastUsedAt,
                    k.RevokedAt))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<IntegrationApiKeyDto>>.Success(rows);
        }
    }
}
