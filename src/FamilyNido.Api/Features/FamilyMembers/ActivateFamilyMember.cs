using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>Slice: re-activate a previously archived member (RF-USR-004).</summary>
public static class ActivateFamilyMember
{
    /// <summary>Command carrying the target id.</summary>
    public sealed record Command(Guid MemberId) : IRequest<Result<FamilyMemberDto>>;

    /// <summary>Handler — flips <c>IsActive</c> to true.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<FamilyMemberDto>>
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
        public async Task<Result<FamilyMemberDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var member = await _db.FamilyMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(
                    m => m.Id == request.MemberId && m.FamilyId == current.Family.Id,
                    cancellationToken);

            if (member is null)
            {
                return ApplicationError.NotFound(
                    "family_member.not_found",
                    $"Member {request.MemberId} not found in family.");
            }

            member.IsActive = true;
            await _db.SaveChangesAsync(cancellationToken);

            return FamilyMemberDto.From(member);
        }
    }
}
