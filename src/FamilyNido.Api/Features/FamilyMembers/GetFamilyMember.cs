using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>Fetches a single member of the caller's family by id (RF-USR-001).</summary>
public static class GetFamilyMember
{
    /// <summary>Query carrying the target member id.</summary>
    public sealed record Query(Guid MemberId) : IRequest<Result<FamilyMemberDto>>;

    /// <summary>Handler. Resolves the calling family before querying.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<FamilyMemberDto>>
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
        public async Task<Result<FamilyMemberDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var member = await _db.FamilyMembers
                .AsNoTracking()
                .Include(m => m.User)
                .FirstOrDefaultAsync(
                    m => m.Id == request.MemberId && m.FamilyId == current.Family.Id,
                    cancellationToken);

            return member is null
                ? ApplicationError.NotFound("family_member.not_found", $"Member {request.MemberId} not found in family.")
                : FamilyMemberDto.From(member);
        }
    }
}
