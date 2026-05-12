using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>Slice: soft-deactivate a member while preserving history (RF-USR-004).</summary>
public static class DeactivateFamilyMember
{
    /// <summary>Command carrying the target id.</summary>
    public sealed record Command(Guid MemberId) : IRequest<Result<FamilyMemberDto>>;

    /// <summary>Handler — enforces the at-least-one-admin invariant.</summary>
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

            if (await WouldLeaveZeroAdminsAsync(_db, member, cancellationToken))
            {
                return ApplicationError.Conflict(
                    "family.must_keep_admin",
                    "Cannot deactivate the last admin of the family.");
            }

            member.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);

            return FamilyMemberDto.From(member);
        }
    }

    /// <summary>
    /// True when <paramref name="member"/> is an active admin AND there are no
    /// other active admins in the family. Shared with <see cref="DeleteFamilyMember"/>.
    /// </summary>
    internal static async Task<bool> WouldLeaveZeroAdminsAsync(
        ApplicationDbContext db,
        FamilyMember member,
        CancellationToken ct)
    {
        if (member.User is null || member.User.Role != FamilyRole.Admin)
        {
            return false;
        }

        var otherActiveAdmins = await db.FamilyMembers
            .CountAsync(m =>
                m.FamilyId == member.FamilyId
                && m.Id != member.Id
                && m.IsActive
                && m.User != null
                && m.User.Role == FamilyRole.Admin,
                ct);

        return otherActiveAdmins == 0;
    }
}
