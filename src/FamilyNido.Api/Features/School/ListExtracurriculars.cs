using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>GET /api/school/extracurriculars?includeArchived=…</c>. Lists
/// the family's after-school activities, optionally including archived rows
/// for the historical view.
/// </summary>
public static class ListExtracurriculars
{
    /// <summary>Query carrying the include-archived flag.</summary>
    public sealed record Query(bool IncludeArchived) : IRequest<Result<IReadOnlyList<ExtracurricularDto>>>;

    /// <summary>Reads the rows from the caller's family.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<ExtracurricularDto>>>
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
        public async Task<Result<IReadOnlyList<ExtracurricularDto>>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var rows = await _db.Extracurriculars
                .AsNoTracking()
                .Where(e => e.FamilyId == current.Family.Id && (request.IncludeArchived || !e.IsArchived))
                .OrderBy(e => e.IsArchived).ThenBy(e => e.Name)
                .ToListAsync(cancellationToken);

            IReadOnlyList<ExtracurricularDto> dtos = rows.Select(e => new ExtracurricularDto(
                e.Id, e.FamilyMemberId, e.Name, e.Location, e.ContactPhone,
                e.WeeklyDays, e.StartTime, e.EndTime, e.StartDate, e.EndDate,
                e.DefaultDropoffMemberId, e.DefaultPickupMemberId, e.Notes, e.IsArchived)).ToList();
            return Result<IReadOnlyList<ExtracurricularDto>>.Success(dtos);
        }
    }
}
