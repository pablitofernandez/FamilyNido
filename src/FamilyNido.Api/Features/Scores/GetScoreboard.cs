using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Scores;

/// <summary>
/// Slice for <c>GET /api/scores/leaderboard?from=&amp;to=</c>. Aggregates every
/// task completion in [from, to] (inclusive, by occurrence date) for the caller's
/// family, joining each completion to its task's current <c>Points</c> value to
/// produce a per-member sum.
/// </summary>
/// <remarks>
/// History is intentionally mutable: editing a task's reward also reshapes past
/// totals because we read <c>task.Points</c> at query time rather than freezing
/// it on the completion row.
/// </remarks>
public static class GetScoreboard
{
    /// <summary>Inclusive [From, To] range query.</summary>
    public sealed record Query(DateOnly From, DateOnly To) : IRequest<Result<LeaderboardDto>>;

    /// <summary>Computes the leaderboard.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<LeaderboardDto>>
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
        public async Task<Result<LeaderboardDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }
            if (request.To < request.From)
            {
                return ApplicationError.Validation("scores.bad_range", "End date must be on or after the start date.");
            }

            var familyId = current.Family.Id;

            // Group by completing member, summing the task's current Points.
            // Anonymous (member-deleted) completions land in a null bucket and
            // are filtered out — there's no scoreboard row to assign them to.
            var rows = await _db.TaskCompletions
                .AsNoTracking()
                .Where(c => c.Task!.FamilyId == familyId
                    && c.OccurrenceDate >= request.From && c.OccurrenceDate <= request.To
                    && c.CompletedByMemberId != null)
                .GroupBy(c => c.CompletedByMemberId!.Value)
                .Select(g => new ScoreboardEntryDto(
                    g.Key,
                    g.Sum(c => c.Task!.Points),
                    g.Count()))
                .ToListAsync(cancellationToken);

            var ordered = rows
                .OrderByDescending(r => r.Points)
                .ThenByDescending(r => r.CompletionCount)
                .ToList();

            return new LeaderboardDto(request.From, request.To, ordered);
        }
    }
}
