using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Integrations;

/// <summary>
/// Slice for <c>POST /api/integrations/api-keys/{id}/revoke</c>. Soft-revokes
/// the token: the row stays for audit but the auth handler refuses to
/// authenticate it from this point on.
/// </summary>
public static class RevokeIntegrationApiKey
{
    /// <summary>Command identifying the token to revoke.</summary>
    public sealed record Command(Guid Id) : IRequest<Result<Unit>>;

    /// <summary>Marks the token as revoked, idempotent on already-revoked rows.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _timeProvider;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext, TimeProvider timeProvider)
        {
            _db = db;
            _userContext = userContext;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var key = await _db.IntegrationApiKeys
                .FirstOrDefaultAsync(
                    k => k.Id == request.Id && k.FamilyId == current.Family.Id,
                    cancellationToken);

            if (key is null)
            {
                return ApplicationError.NotFound("integration_api_key.not_found", "Token not found.");
            }

            // Idempotent: revoking an already-revoked token is a no-op success
            // so the UI can let users hammer the button without surprises.
            if (key.RevokedAt is null)
            {
                key.RevokedAt = _timeProvider.GetUtcNow();
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Unit.Value;
        }
    }
}
