using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Scores;

/// <summary>
/// Slice for <c>GET /api/scores/members/{memberId}/history?limit=N</c>. Returns
/// the most recent task completions of one member, ordered by completion
/// instant descending. Used by the per-member detail page to render the
/// "historial de tareas hechas" list.
/// </summary>
public static class GetMemberCompletionHistory
{
    /// <summary>Default page size — fits comfortably in a /nido/:id section.</summary>
    public const int DefaultLimit = 50;

    /// <summary>Hard cap to avoid pathological pulls.</summary>
    public const int MaxLimit = 200;

    /// <summary>Query carrying the member id and an optional limit.</summary>
    public sealed record Query(Guid MemberId, int Limit) : IRequest<Result<IReadOnlyList<MemberCompletionDto>>>;

    /// <summary>Reads the rows.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<MemberCompletionDto>>>
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
        public async Task<Result<IReadOnlyList<MemberCompletionDto>>> HandleAsync(Query request, CancellationToken cancellationToken)
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

            var limit = Math.Clamp(request.Limit <= 0 ? DefaultLimit : request.Limit, 1, MaxLimit);

            // Filter on the family of the owning task so a deleted member's
            // completions across moved-out families never leak. Sorted by the
            // completion instant — that's the natural reading order.
            var rows = await _db.TaskCompletions
                .AsNoTracking()
                .Where(c => c.CompletedByMemberId == request.MemberId
                    && c.Task!.FamilyId == current.Family.Id)
                .OrderByDescending(c => c.CompletedAt)
                .Take(limit)
                .Select(c => new MemberCompletionDto(
                    c.TaskId,
                    c.Task!.Title,
                    c.OccurrenceDate,
                    c.CompletedAt,
                    c.Task!.Points))
                .ToListAsync(cancellationToken);

            return rows.AsReadOnly();
        }
    }
}
