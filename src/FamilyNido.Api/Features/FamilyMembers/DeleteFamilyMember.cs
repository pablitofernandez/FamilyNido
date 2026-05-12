using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>Slice: hard-delete a member, preserving the at-least-one-admin invariant (RF-USR-004).</summary>
public static class DeleteFamilyMember
{
    /// <summary>Command carrying the target id.</summary>
    public sealed record Command(Guid MemberId) : IRequest<Result<Unit>>;

    /// <summary>Handler — refuses if the target is the last admin.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
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
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken cancellationToken)
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

            if (await DeactivateFamilyMember.WouldLeaveZeroAdminsAsync(_db, member, cancellationToken))
            {
                return ApplicationError.Conflict(
                    "family.must_keep_admin",
                    "Cannot delete the last admin of the family.");
            }

            _db.FamilyMembers.Remove(member);
            await _db.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
