using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Meals;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Meals;

/// <summary>
/// Slice: copy the previous week's slots into the target week. By default only
/// fills slots that are empty in the destination week; <c>Overwrite=true</c>
/// replaces non-empty slots too. Returns the resulting week as a single round-trip.
/// </summary>
public static class DuplicatePreviousWeek
{
    /// <summary>Command — week to populate + overwrite policy.</summary>
    /// <param name="WeekStart">Any date inside the target week (snapped to Monday).</param>
    /// <param name="Overwrite">When true, also replaces destination slots that already had a name.</param>
    public sealed record Command(DateOnly WeekStart, bool Overwrite) : IRequest<Result<MealWeekDto>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<MealWeekDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly IMediator _mediator;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext, IMediator mediator)
        {
            _db = db;
            _userContext = userContext;
            _mediator = mediator;
        }

        /// <inheritdoc />
        public async Task<Result<MealWeekDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var targetMonday = WeekStart.SnapToMonday(request.WeekStart);
            var sourceMonday = targetMonday.AddDays(-7);
            var sourceSunday = targetMonday.AddDays(-1);
            var targetSunday = targetMonday.AddDays(6);

            var sourceRows = await _db.MealPlanSlots
                .AsNoTracking()
                .Where(s => s.FamilyId == current.Family.Id &&
                            s.Date >= sourceMonday && s.Date <= sourceSunday)
                .ToListAsync(cancellationToken);

            if (sourceRows.Count == 0)
            {
                // Nothing to copy — short-circuit and return the (likely empty) week.
                return await _mediator.SendAsync(new GetWeekPlan.Query(targetMonday), cancellationToken);
            }

            var existingTarget = await _db.MealPlanSlots
                .Where(s => s.FamilyId == current.Family.Id &&
                            s.Date >= targetMonday && s.Date <= targetSunday)
                .ToListAsync(cancellationToken);

            var existingByKey = existingTarget.ToDictionary(s => (s.Date, s.Slot));

            foreach (var source in sourceRows)
            {
                var targetDate = source.Date.AddDays(7);
                var key = (targetDate, source.Slot);

                if (existingByKey.TryGetValue(key, out var destination))
                {
                    // Per-course merge: each course is treated independently so we
                    // never half-replace a slot.
                    if (request.Overwrite || destination.FirstCourse is null)
                    {
                        if (source.FirstCourse is not null)
                        {
                            destination.FirstCourse = source.FirstCourse;
                        }
                    }
                    if (request.Overwrite || destination.SecondCourse is null)
                    {
                        if (source.SecondCourse is not null)
                        {
                            destination.SecondCourse = source.SecondCourse;
                        }
                    }
                }
                else
                {
                    _db.MealPlanSlots.Add(new MealPlanSlot
                    {
                        FamilyId = current.Family.Id,
                        Date = targetDate,
                        Slot = source.Slot,
                        FirstCourse = source.FirstCourse,
                        SecondCourse = source.SecondCourse,
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            return await _mediator.SendAsync(new GetWeekPlan.Query(targetMonday), cancellationToken);
        }
    }
}
