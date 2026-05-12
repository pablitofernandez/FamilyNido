using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Slice: read events for the family calendar within a time window. Filters cover
/// the day/week/month/agenda views by adjusting <c>from</c>/<c>to</c> on the client side.
/// </summary>
public static class ListEvents
{
    /// <summary>Query — half-open time window, optional member filter.</summary>
    /// <param name="From">Inclusive lower bound (UTC).</param>
    /// <param name="To">Exclusive upper bound (UTC).</param>
    /// <param name="MemberIds">Optional set of family member ids; null/empty means "all members".</param>
    public sealed record Query(
        DateTimeOffset From,
        DateTimeOffset To,
        IReadOnlyList<Guid>? MemberIds) : IRequest<Result<IReadOnlyList<CalendarEventDto>>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Query>
    {
        /// <summary>Creates the validator.</summary>
        public Validator()
        {
            RuleFor(x => x.To).GreaterThan(x => x.From);

            RuleFor(x => x.To.Subtract(x.From))
                .LessThanOrEqualTo(TimeSpan.FromDays(370))
                .WithMessage("Time window cannot exceed 370 days.");
        }
    }

    /// <summary>Handler — runs the family-scoped range query.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<CalendarEventDto>>>
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
        public async Task<Result<IReadOnlyList<CalendarEventDto>>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            // Standard "overlap" predicate: events where start < to AND end > from.
            var events = _db.CalendarEvents
                .AsNoTracking()
                .Include(e => e.LinkedCalendar)
                .Include(e => e.RelatedMembers)
                .Where(e => e.FamilyId == current.Family.Id)
                .Where(e => e.StartAt < request.To && e.EndAt > request.From);

            if (request.MemberIds is { Count: > 0 } members)
            {
                var memberSet = members.ToHashSet();
                // OR across two paths: the event surfaces on the per-member view
                // either because its source calendar is bound to the member
                // (RF-CAL-007) or because it has been explicitly tagged with
                // them at the event level (RF-CAL-013).
                events = events.Where(e =>
                    (e.LinkedCalendar!.FamilyMemberId.HasValue
                        && memberSet.Contains(e.LinkedCalendar.FamilyMemberId.Value))
                    || e.RelatedMembers.Any(m => memberSet.Contains(m.Id)));
            }

            var rows = await events
                .OrderBy(e => e.StartAt)
                .ThenBy(e => e.Title)
                .ToListAsync(cancellationToken);

            IReadOnlyList<CalendarEventDto> dto = [.. rows.Select(CalendarEventDto.From)];
            return Result<IReadOnlyList<CalendarEventDto>>.Success(dto);
        }
    }
}
