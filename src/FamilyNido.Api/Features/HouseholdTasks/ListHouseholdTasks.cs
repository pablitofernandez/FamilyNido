using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: list tasks of the current user's family (RF-TASK-006).</summary>
public static class ListHouseholdTasks
{
    /// <summary>Query carrying optional filters.</summary>
    /// <param name="IncludeArchived">When true, archived rows are also returned.</param>
    /// <param name="MemberId">
    /// When set, only return tasks in which this member appears — either as the
    /// responsible (singular) or as one of the related members (N:M). The two
    /// roles surface together so the per-member dashboard can show one list.
    /// </param>
    public sealed record Query(bool IncludeArchived, Guid? MemberId)
        : IRequest<Result<IReadOnlyList<HouseholdTaskDto>>>;

    /// <summary>Handler backed by EF Core.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<HouseholdTaskDto>>>
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
        public async Task<Result<IReadOnlyList<HouseholdTaskDto>>> HandleAsync(
            Query request,
            CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var query = _db.HouseholdTasks
                .AsNoTracking()
                .Include(t => t.RelatedMembers)
                .Where(t => t.FamilyId == current.Family.Id);

            if (!request.IncludeArchived)
            {
                query = query.Where(t => !t.IsArchived);
            }

            if (request.MemberId is { } memberId)
            {
                // OR across both roles: a member's dashboard shows tasks they
                // execute (responsible) and tasks that concern them (related).
                query = query.Where(t =>
                    t.ResponsibleMemberId == memberId
                    || t.RelatedMembers.Any(a => a.Id == memberId));
            }

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            // For the "Todas" tab in the UI, attach the most recent completion of
            // each task — one row per task, the latest by completed_at. Cheap
            // single round trip with a window-by-task GROUP BY.
            var taskIds = tasks.Select(t => t.Id).ToList();
            var latestRaw = taskIds.Count == 0
                ? []
                : await _db.TaskCompletions
                    .AsNoTracking()
                    .Where(c => taskIds.Contains(c.TaskId))
                    .GroupBy(c => c.TaskId)
                    .Select(g => g.OrderByDescending(c => c.CompletedAt).First())
                    .ToListAsync(cancellationToken);
            var latestByTask = latestRaw.ToDictionary(c => c.TaskId);

            IReadOnlyList<HouseholdTaskDto> dto = [..
                tasks.Select(t => HouseholdTaskDto.From(
                    t,
                    latestByTask.TryGetValue(t.Id, out var c)
                        ? new LatestCompletionDto(c.OccurrenceDate, c.CompletedByMemberId, c.CompletedAt)
                        : null))];
            return Result<IReadOnlyList<HouseholdTaskDto>>.Success(dto);
        }
    }
}
