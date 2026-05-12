using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>DELETE /api/school/day-schedule/exceptions/{memberId}/{date}</c>.
/// Removes the override and falls back to the weekly schedule.
/// </summary>
public static class RemoveSchoolDayException
{
    /// <summary>Command identifying the row to delete.</summary>
    public sealed record Command(Guid MemberId, DateOnly Date) : IRequest<Result<Unit>>;

    /// <summary>Empty success payload.</summary>
    public sealed record Unit;

    /// <summary>Removes the row when present; idempotent when absent.</summary>
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

            var entry = await _db.SchoolDayExceptions
                .FirstOrDefaultAsync(e => e.FamilyMemberId == request.MemberId && e.Date == request.Date, cancellationToken);

            if (entry is not null)
            {
                if (entry.FamilyId != current.Family.Id)
                {
                    return ApplicationError.NotFound("school.day_schedule.exception_not_found", "Exception not found.");
                }
                _db.SchoolDayExceptions.Remove(entry);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new Unit();
        }
    }
}
