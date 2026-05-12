using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Scores;

/// <summary>
/// Slice for <c>GET /api/scores/members/{memberId}</c>. Returns a member's
/// reward totals for three rolling windows: this ISO week (Monday→Sunday in
/// the server's local time zone), this calendar month, and all-time.
/// </summary>
public static class GetMemberScore
{
    /// <summary>Query carrying the target member.</summary>
    public sealed record Query(Guid MemberId) : IRequest<Result<MemberScoreDto>>;

    /// <summary>Computes the totals.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<MemberScoreDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _time;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext, TimeProvider time)
        {
            _db = db;
            _userContext = userContext;
            _time = time;
        }

        /// <inheritdoc />
        public async Task<Result<MemberScoreDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var memberInFamily = await _db.FamilyMembers
                .AnyAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, cancellationToken);
            if (!memberInFamily)
            {
                return ApplicationError.NotFound("scores.unknown_member", "Member not found.");
            }

            // Compute the relevant date windows. "This week" anchors on Monday
            // (Spanish convention); "this month" is the calendar month of today.
            var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
            var dow = (int)today.DayOfWeek; // Sunday = 0
            var daysFromMonday = dow == 0 ? 6 : dow - 1;
            var weekStart = today.AddDays(-daysFromMonday);
            var monthStart = new DateOnly(today.Year, today.Month, 1);

            // Single round-trip: pull every completion of this member with its
            // task's points + occurrence date, then aggregate in memory. The
            // sum of completions per member rarely exceeds a few hundred rows,
            // so the cost is negligible and we avoid running three SQL queries.
            var rows = await _db.TaskCompletions
                .AsNoTracking()
                .Where(c => c.CompletedByMemberId == request.MemberId
                    && c.Task!.FamilyId == current.Family.Id)
                .Select(c => new { c.OccurrenceDate, Points = c.Task!.Points })
                .ToListAsync(cancellationToken);

            var thisWeek = rows.Where(r => r.OccurrenceDate >= weekStart).Sum(r => r.Points);
            var thisMonth = rows.Where(r => r.OccurrenceDate >= monthStart).Sum(r => r.Points);
            var allTime = rows.Sum(r => r.Points);

            return new MemberScoreDto(request.MemberId, thisWeek, thisMonth, allTime);
        }
    }
}
