using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>GET /api/school/members/{memberId}/profile</c>. Returns the
/// stored card or null when the kid has no profile yet.
/// </summary>
public static class GetSchoolProfile
{
    /// <summary>Query carrying the member id.</summary>
    public sealed record Query(Guid MemberId) : IRequest<Result<SchoolProfileDto?>>;

    /// <summary>Validates family scope and returns the profile DTO (nullable).</summary>
    public sealed class Handler : IRequestHandler<Query, Result<SchoolProfileDto?>>
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
        public async Task<Result<SchoolProfileDto?>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var memberOk = await _db.FamilyMembers
                .AnyAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, cancellationToken);
            if (!memberOk)
            {
                return ApplicationError.NotFound("family_member.not_found", $"Member {request.MemberId} not found.");
            }

            var profile = await _db.SchoolProfiles
                .AsNoTracking()
                .Where(p => p.FamilyMemberId == request.MemberId)
                .Select(p => new SchoolProfileDto(
                    p.SchoolName,
                    p.Grade,
                    p.Tutor,
                    p.TransportMode,
                    p.MorningTime,
                    p.AfternoonTime,
                    p.Notes))
                .FirstOrDefaultAsync(cancellationToken);

            return profile;
        }
    }
}
