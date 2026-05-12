using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Health;

/// <summary>
/// Slice for <c>GET /api/health/members/{memberId}</c>. Returns the member's
/// health card with profile, vaccinations and medications in a single
/// request — the screen needs all three at once so paging would buy nothing.
/// </summary>
public static class GetMemberHealth
{
    /// <summary>Query carrying the target member id.</summary>
    public sealed record Query(Guid MemberId) : IRequest<Result<MemberHealthDto>>;

    /// <summary>Resolves the member, validates family scope, hydrates the DTO.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<MemberHealthDto>>
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
        public async Task<Result<MemberHealthDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var member = await _db.FamilyMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, cancellationToken);
            if (member is null)
            {
                return ApplicationError.NotFound("family_member.not_found", $"Member {request.MemberId} not found.");
            }

            var profile = await _db.HealthProfiles
                .AsNoTracking()
                .Where(p => p.FamilyMemberId == request.MemberId)
                .Select(p => new HealthProfileDto(p.BloodType, p.Allergies, p.ChronicConditions, p.Notes))
                .FirstOrDefaultAsync(cancellationToken);

            var vaccinations = await _db.Vaccinations
                .AsNoTracking()
                .Where(v => v.FamilyMemberId == request.MemberId)
                .OrderByDescending(v => v.Date)
                .Select(v => new VaccinationDto(v.Id, v.Name, v.Date, v.NextDueDate, v.Notes))
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var medications = await _db.Medications
                .AsNoTracking()
                .Where(m => m.FamilyMemberId == request.MemberId)
                .OrderByDescending(m => m.StartDate)
                .Select(m => new MedicationDto(
                    m.Id,
                    m.Name,
                    m.Dose,
                    m.Frequency,
                    m.StartDate,
                    m.EndDate,
                    m.Instructions,
                    m.EndDate == null || m.EndDate >= today))
                .ToListAsync(cancellationToken);

            return new MemberHealthDto(member.Id, member.DisplayName, profile, vaccinations, medications);
        }
    }
}
