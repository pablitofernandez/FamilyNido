using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>Lists members of the current user's family (RF-USR-001).</summary>
public static class ListFamilyMembers
{
    /// <summary>Query carrying the optional "include archived" flag.</summary>
    /// <param name="IncludeInactive">When true, archived rows are also returned.</param>
    public sealed record Query(bool IncludeInactive) : IRequest<Result<IReadOnlyList<FamilyMemberDto>>>;

    /// <summary>EF Core-backed handler. Requires the caller to be linked.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<FamilyMemberDto>>>
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
        public async Task<Result<IReadOnlyList<FamilyMemberDto>>> HandleAsync(
            Query request,
            CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var now = _timeProvider.GetUtcNow();

            var query = _db.FamilyMembers
                .AsNoTracking()
                .Include(m => m.User)
                .Where(m => m.FamilyId == current.Family.Id);

            if (!request.IncludeInactive)
            {
                query = query.Where(m => m.IsActive);
            }

            // Project each member with its most-recent live invitation (if any)
            // in a single SQL round-trip — avoids the N+1 we'd get if the front
            // had to ask "is there a pending invitation?" per row.
            var rows = await query
                .OrderBy(m => m.MemberType)
                .ThenBy(m => m.DisplayName)
                .Select(m => new
                {
                    Member = m,
                    Pending = _db.Invitations
                        .Where(i => i.FamilyMemberId == m.Id
                            && i.ConsumedAt == null
                            && i.RevokedAt == null
                            && i.ExpiresAt > now)
                        .OrderByDescending(i => i.CreatedAt)
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            IReadOnlyList<FamilyMemberDto> dto = [.. rows.Select(r => FamilyMemberDto.From(r.Member, r.Pending))];
            return Result<IReadOnlyList<FamilyMemberDto>>.Success(dto);
        }
    }
}
