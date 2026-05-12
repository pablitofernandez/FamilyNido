using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Slice: an authenticated (already-OIDC-logged) caller redeems an invitation
/// to be linked to a <see cref="Domain.Families.FamilyMember"/>. The redemption
/// is performed with a conditional UPDATE so two concurrent clicks cannot
/// consume the same token twice.
/// </summary>
public static class AcceptInvitationOidc
{
    /// <summary>Command carrying the raw token from the URL.</summary>
    public sealed record Command(string Token) : IRequest<Result<AcceptInvitationResponse>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<AcceptInvitationResponse>>
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
        public async Task<Result<AcceptInvitationResponse>> HandleAsync(Command request, CancellationToken ct)
        {
            var user = await _userContext.GetUserAsync(ct);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Caller is not authenticated.");
            }

            // The user might already be linked to another family member —
            // refuse rather than steal the existing binding silently.
            var alreadyLinked = await _db.FamilyMembers.AnyAsync(m => m.UserId == user.Id, ct);
            if (alreadyLinked)
            {
                return ApplicationError.Conflict(
                    "user.already_linked",
                    "Your account is already linked to a family member.");
            }

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return ApplicationError.NotFound("invitation.not_found", "Invitation not found.");
            }

            var hash = InvitationToken.Hash(request.Token);
            var invitation = await _db.Invitations
                .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);

            if (invitation is null)
            {
                return ApplicationError.NotFound("invitation.not_found", "Invitation not found.");
            }

            // Conditional update: only consume the row if it's still pending.
            // We piggyback on EF's optimistic update by checking ConsumedAt /
            // RevokedAt / ExpiresAt within the WHERE (translated to SQL) and
            // verifying the affected row count.
            var now = _timeProvider.GetUtcNow();
            var consumedRows = await _db.Invitations
                .Where(i => i.Id == invitation.Id
                    && i.ConsumedAt == null
                    && i.RevokedAt == null
                    && i.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.ConsumedAt, now)
                    .SetProperty(i => i.ConsumedByUserId, user.Id), ct);

            if (consumedRows == 0)
            {
                return ApplicationError.Conflict(
                    "invitation.unavailable",
                    "This invitation has already been used, expired, or was revoked.");
            }

            // Bind member → user (final step). If a stray race had bound the
            // member to someone else between our read and write, the
            // single-row update with the strict predicate below catches it.
            var memberRows = await _db.FamilyMembers
                .Where(m => m.Id == invitation.FamilyMemberId && m.UserId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.UserId, (Guid?)user.Id), ct);

            if (memberRows == 0)
            {
                // Compensate: roll back the consumed flag so the admin can
                // reissue. ExecuteUpdate is auto-committed so we can't undo
                // via transaction here — settle for clearing ConsumedAt.
                await _db.Invitations
                    .Where(i => i.Id == invitation.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(i => i.ConsumedAt, (DateTimeOffset?)null)
                        .SetProperty(i => i.ConsumedByUserId, (Guid?)null), ct);

                return ApplicationError.Conflict(
                    "family_member.already_linked",
                    "Target member is already linked to another user. Ask the admin to reissue the invitation.");
            }

            // Promote the user to the role configured at invitation time.
            // Adult covers the common case; Admin grants the management UI.
            var trackedUser = await _db.Users.FirstAsync(u => u.Id == user.Id, ct);
            trackedUser.Role = invitation.RoleOnAccept;
            trackedUser.LastLoginAt = now;
            await _db.SaveChangesAsync(ct);

            return new AcceptInvitationResponse(
                FamilyMemberId: invitation.FamilyMemberId,
                FamilyId: invitation.FamilyId,
                Role: invitation.RoleOnAccept);
        }
    }
}

/// <summary>Outcome of a successful invitation redemption.</summary>
/// <param name="FamilyMemberId">Member the user is now linked to.</param>
/// <param name="FamilyId">Owning family of that member.</param>
/// <param name="Role">Role granted to the user.</param>
public sealed record AcceptInvitationResponse(
    Guid FamilyMemberId,
    Guid FamilyId,
    Domain.Families.FamilyRole Role);
