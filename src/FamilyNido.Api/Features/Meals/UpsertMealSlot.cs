using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Meals;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Meals;

/// <summary>
/// Slice: insert or update a single course within a meal slot. Idempotent —
/// rewriting the same course with the same name leaves the row otherwise
/// unchanged. The other course (the one not addressed by this command) is
/// preserved.
/// </summary>
public static class UpsertMealSlot
{
    /// <summary>Command — addresses a single course within a slot.</summary>
    /// <param name="Date">Date of the slot.</param>
    /// <param name="Slot">Slot of the day (Lunch/Dinner).</param>
    /// <param name="Course">Which course to write (First/Second).</param>
    /// <param name="Name">Free-text name (1..120 chars).</param>
    public sealed record Command(
        DateOnly Date,
        MealSlot Slot,
        MealCourse Course,
        string Name) : IRequest<Result<MealPlanSlotDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator.</summary>
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(120);
        }
    }

    /// <summary>Handler — locates the existing row by unique key or inserts a new one.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<MealPlanSlotDto>>
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
        public async Task<Result<MealPlanSlotDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var trimmed = request.Name.Trim();

            var existing = await _db.MealPlanSlots
                .FirstOrDefaultAsync(
                    s => s.FamilyId == current.Family.Id &&
                         s.Date == request.Date &&
                         s.Slot == request.Slot,
                    cancellationToken);

            if (existing is null)
            {
                existing = new MealPlanSlot
                {
                    FamilyId = current.Family.Id,
                    Date = request.Date,
                    Slot = request.Slot,
                    FirstCourse = request.Course == MealCourse.First ? trimmed : null,
                    SecondCourse = request.Course == MealCourse.Second ? trimmed : null,
                };
                _db.MealPlanSlots.Add(existing);
            }
            else
            {
                if (request.Course == MealCourse.First)
                {
                    existing.FirstCourse = trimmed;
                }
                else
                {
                    existing.SecondCourse = trimmed;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return MealPlanSlotDto.From(existing);
        }
    }
}
