using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Slice: lists pending invitations (not consumed, not revoked, not expired)
/// in the caller's family. Used by the admin "pending invitations" panel.
/// </summary>
public static class ListInvitations
{
    /// <summary>Query — no inputs; the caller's family is implicit.</summary>
    public sealed record Query : IRequest<Result<IReadOnlyList<InvitationDto>>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<InvitationDto>>>
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
        public async Task<Result<IReadOnlyList<InvitationDto>>> HandleAsync(Query request, CancellationToken ct)
        {
            var current = await _userContext.GetAsync(ct);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var now = _timeProvider.GetUtcNow();

            var rows = await _db.Invitations
                .AsNoTracking()
                .Include(i => i.FamilyMember)
                .Where(i => i.FamilyId == current.Family.Id
                    && i.ConsumedAt == null
                    && i.RevokedAt == null
                    && i.ExpiresAt > now)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync(ct);

            IReadOnlyList<InvitationDto> dto = [.. rows.Select(i => new InvitationDto(
                Id: i.Id,
                FamilyMemberId: i.FamilyMemberId,
                MemberDisplayName: i.FamilyMember?.DisplayName ?? "",
                Email: i.Email,
                RoleOnAccept: i.RoleOnAccept,
                ExpiresAt: i.ExpiresAt,
                CreatedAt: i.CreatedAt))];

            return Result<IReadOnlyList<InvitationDto>>.Success(dto);
        }
    }
}
