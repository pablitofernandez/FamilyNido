using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Agenda;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.MemberAgenda;

/// <summary>
/// Slice for <c>GET /api/member-agenda/overview?from=&amp;to=</c>. Returns every
/// pattern + exception in the family plus a day-by-day "resolved" view that
/// merges them. The frontend shows the resolved list as-is (panel, member
/// detail page) and the raw lists when editing.
/// </summary>
public static class GetMemberAgendaOverview
{
    /// <summary>Inclusive [From, To] range query.</summary>
    public sealed record Query(DateOnly From, DateOnly To) : IRequest<Result<MemberAgendaOverviewDto>>;

    /// <summary>Composes the overview DTO.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<MemberAgendaOverviewDto>>
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
        public async Task<Result<MemberAgendaOverviewDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }
            if (request.To < request.From)
            {
                return ApplicationError.Validation("agenda.overview.bad_range", "End date must be on or after the start date.");
            }

            var familyId = current.Family.Id;

            var patterns = await _db.MemberAgendaPatterns
                .AsNoTracking()
                .Where(p => p.FamilyId == familyId)
                .OrderBy(p => p.FamilyMemberId).ThenBy(p => p.DayOfWeek).ThenBy(p => p.StartTime)
                .ToListAsync(cancellationToken);

            var exceptions = await _db.MemberAgendaExceptions
                .AsNoTracking()
                .Where(e => e.FamilyId == familyId && e.Date >= request.From && e.Date <= request.To)
                .OrderBy(e => e.Date)
                .ToListAsync(cancellationToken);

            var patternDtos = patterns
                .Select(p => new MemberAgendaPatternDto(
                    p.Id, p.FamilyMemberId, p.DayOfWeek, p.Label, p.Location,
                    p.StartTime, p.EndTime, p.TransportMode, p.IsAway, p.Notes, p.IsActive))
                .ToList();

            var exceptionDtos = exceptions
                .Select(e => new MemberAgendaExceptionDto(
                    e.Id, e.FamilyMemberId, e.Date, e.PatternId, e.IsCancelled, e.Label, e.Location,
                    e.StartTime, e.EndTime, e.TransportMode, e.IsAway, e.Notes))
                .ToList();

            var resolved = Resolve(patterns, exceptions, request.From, request.To);

            return new MemberAgendaOverviewDto(request.From, request.To, patternDtos, exceptionDtos, resolved);
        }

        /// <summary>
        /// Build day-by-day resolved entries. For each date in range:
        ///   1) Take active patterns whose weekday matches.
        ///   2) Drop patterns that have a cancelling exception on that date.
        ///   3) Apply non-cancel exceptions as overrides on top of their pattern.
        ///   4) Append ad-hoc exceptions (PatternId null) as standalone entries.
        /// </summary>
        internal static IReadOnlyList<ResolvedAgendaEntryDto> Resolve(
            IReadOnlyList<MemberAgendaPattern> patterns,
            IReadOnlyList<MemberAgendaException> exceptions,
            DateOnly from,
            DateOnly to)
        {
            var byPattern = exceptions
                .Where(e => e.PatternId is not null)
                .ToDictionary(e => (e.PatternId!.Value, e.Date));

            var result = new List<ResolvedAgendaEntryDto>();

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                foreach (var pattern in patterns)
                {
                    if (!pattern.IsActive) continue;
                    if (pattern.DayOfWeek != date.DayOfWeek) continue;

                    if (byPattern.TryGetValue((pattern.Id, date), out var ex))
                    {
                        if (ex.IsCancelled) continue;
                        result.Add(new ResolvedAgendaEntryDto(
                            pattern.FamilyMemberId, date, pattern.Id, ex.Id,
                            ex.Label ?? pattern.Label,
                            ex.Location ?? pattern.Location,
                            ex.StartTime ?? pattern.StartTime,
                            ex.EndTime ?? pattern.EndTime,
                            ex.TransportMode ?? pattern.TransportMode,
                            ex.IsAway ?? pattern.IsAway,
                            ex.Notes ?? pattern.Notes));
                        continue;
                    }

                    result.Add(new ResolvedAgendaEntryDto(
                        pattern.FamilyMemberId, date, pattern.Id, ExceptionId: null,
                        pattern.Label, pattern.Location, pattern.StartTime, pattern.EndTime,
                        pattern.TransportMode, pattern.IsAway, pattern.Notes));
                }
            }

            // Ad-hoc exceptions: PatternId is null. They must fall in range
            // (already filtered by the SQL query) and contribute regardless of
            // weekday. Cancelled ad-hocs are nonsense and ignored.
            foreach (var ex in exceptions.Where(e => e.PatternId is null && !e.IsCancelled))
            {
                result.Add(new ResolvedAgendaEntryDto(
                    ex.FamilyMemberId, ex.Date, PatternId: null, ex.Id,
                    ex.Label ?? "(sin etiqueta)",
                    ex.Location,
                    ex.StartTime,
                    ex.EndTime,
                    ex.TransportMode ?? AgendaTransportMode.None,
                    ex.IsAway ?? true,
                    ex.Notes));
            }

            return result
                .OrderBy(r => r.Date)
                .ThenBy(r => r.MemberId)
                .ThenBy(r => r.StartTime ?? TimeOnly.MinValue)
                .ToList();
        }
    }
}
