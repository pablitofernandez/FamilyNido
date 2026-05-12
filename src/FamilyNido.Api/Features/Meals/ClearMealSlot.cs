using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Meals;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Meals;

/// <summary>
/// Slice: clear a single course of a meal slot. Idempotent — a missing row or
/// already-null column is success. If, after clearing, both courses end up null,
/// the row itself is deleted to keep the table from holding tombstones.
/// </summary>
public static class ClearMealSlot
{
    /// <summary>Command — coordinates the course to clear.</summary>
    public sealed record Command(DateOnly Date, MealSlot Slot, MealCourse Course) : IRequest<Result<Unit>>;

    /// <summary>Carries no value; signals "completed" to the endpoint.</summary>
    public sealed record Unit;

    /// <summary>Handler.</summary>
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
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var existing = await _db.MealPlanSlots
                .FirstOrDefaultAsync(
                    s => s.FamilyId == current.Family.Id &&
                         s.Date == request.Date &&
                         s.Slot == request.Slot,
                    cancellationToken);

            if (existing is null)
            {
                return new Unit();
            }

            if (request.Course == MealCourse.First)
            {
                existing.FirstCourse = null;
            }
            else
            {
                existing.SecondCourse = null;
            }

            // Drop the row entirely when both courses are gone so the table
            // doesn't accumulate empty tombstones.
            if (existing.FirstCourse is null && existing.SecondCourse is null)
            {
                _db.MealPlanSlots.Remove(existing);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return new Unit();
        }
    }
}
