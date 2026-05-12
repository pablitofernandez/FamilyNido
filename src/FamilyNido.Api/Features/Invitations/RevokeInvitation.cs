using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Slice: marks an invitation as revoked. Idempotent on already-revoked rows;
/// returns 409 on already-consumed ones (a consumed invitation cannot be
/// "un-redeemed").
/// </summary>
public static class RevokeInvitation
{
    /// <summary>Command carrying the invitation id.</summary>
    /// <param name="InvitationId">Identifier of the invitation to revoke.</param>
    public sealed record Command(Guid InvitationId) : IRequest<Result<Unit>>;

    /// <summary>Handler.</summary>
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
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken ct)
        {
            var current = await _userContext.GetAsync(ct);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var invitation = await _db.Invitations
                .FirstOrDefaultAsync(
                    i => i.Id == request.InvitationId && i.FamilyId == current.Family.Id,
                    ct);

            if (invitation is null)
            {
                return ApplicationError.NotFound("invitation.not_found", "Invitation does not exist.");
            }

            if (invitation.ConsumedAt is not null)
            {
                return ApplicationError.Conflict(
                    "invitation.already_consumed",
                    "Cannot revoke an invitation that has already been consumed.");
            }

            if (invitation.RevokedAt is null)
            {
                invitation.RevokedAt = _timeProvider.GetUtcNow();
                await _db.SaveChangesAsync(ct);
            }

            return Unit.Value;
        }
    }
}
