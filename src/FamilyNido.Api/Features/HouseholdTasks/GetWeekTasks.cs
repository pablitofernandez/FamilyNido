using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>
/// Slice: 7-day view starting at <see cref="Query.StartDate"/> (defaults to today).
/// The week is always 7 consecutive days starting at the given Monday/day; the caller
/// picks the start date because weeks are culturally variable (Mon vs Sun).
/// </summary>
public static class GetWeekTasks
{
    /// <summary>Query carrying the optional start date.</summary>
    /// <param name="StartDate">First day of the 7-day window. Null ⇒ today in family TZ.</param>
    public sealed record Query(DateOnly? StartDate) : IRequest<Result<IReadOnlyList<DayTasksDto>>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<DayTasksDto>>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _clock;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext, TimeProvider clock)
        {
            _db = db;
            _userContext = userContext;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<Result<IReadOnlyList<DayTasksDto>>> HandleAsync(
            Query request,
            CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var today = GetTodayTasks.Handler.TodayInFamilyZone(_clock, current.Family.TimeZone);
            var from = request.StartDate ?? today;
            var to = from.AddDays(6);

            // Floating tasks are anchored on the family's "today" only when it
            // falls within the requested week — otherwise the past/future week
            // wouldn't carry them.
            var anchor = today >= from && today <= to ? (DateOnly?)today : null;

            var days = await GetTodayTasks.Handler.LoadRangeAsync(
                _db, current.Family.Id, from, to, anchor, cancellationToken);

            IReadOnlyList<DayTasksDto> result = days;
            return Result<IReadOnlyList<DayTasksDto>>.Success(result);
        }
    }
}
