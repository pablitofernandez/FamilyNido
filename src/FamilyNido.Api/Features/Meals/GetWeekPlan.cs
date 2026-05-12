using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Meals;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Meals;

/// <summary>Slice: fetch the meal plan for the week that contains the supplied start date.</summary>
public static class GetWeekPlan
{
    /// <summary>Query — any date works; the handler snaps to the Monday of that week.</summary>
    /// <param name="StartDate">Any date inside the desired week.</param>
    public sealed record Query(DateOnly StartDate) : IRequest<Result<MealWeekDto>>;

    /// <summary>Handler — composes the 7-day grid from the persisted rows.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<MealWeekDto>>
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
        public async Task<Result<MealWeekDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var monday = WeekStart.SnapToMonday(request.StartDate);
            var sunday = monday.AddDays(6);

            var rows = await _db.MealPlanSlots
                .AsNoTracking()
                .Where(s => s.FamilyId == current.Family.Id && s.Date >= monday && s.Date <= sunday)
                .ToListAsync(cancellationToken);

            // Index by (date, slot) for O(1) lookup when assembling the grid.
            var byKey = rows.ToDictionary(r => (r.Date, r.Slot));

            var days = new List<MealDayDto>(7);
            for (var i = 0; i < 7; i++)
            {
                var date = monday.AddDays(i);
                byKey.TryGetValue((date, MealSlot.Lunch), out var lunch);
                byKey.TryGetValue((date, MealSlot.Dinner), out var dinner);
                days.Add(new MealDayDto(
                    date,
                    lunch is null ? null : MealPlanSlotDto.From(lunch),
                    dinner is null ? null : MealPlanSlotDto.From(dinner)));
            }

            return new MealWeekDto(monday, days);
        }
    }
}
