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
    /// <summary>Default page size when the caller does not specify one.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Maximum page size accepted (anything higher is clamped).</summary>
    public const int MaxPageSize = 100;

    /// <summary>Query carrying optional filters and pagination.</summary>
    /// <param name="IncludeArchived">When true, archived rows are also returned.</param>
    /// <param name="MemberId">
    /// When set, only return tasks in which this member appears — either as the
    /// responsible (singular) or as one of the related members (N:M). The two
    /// roles surface together so the per-member dashboard can show one list.
    /// </param>
    /// <param name="Search">
    /// Optional free-text filter (case-insensitive). Matches against Title,
    /// Description and Category. Whitespace-only is treated as no filter.
    /// </param>
    /// <param name="Page">1-based page index; values &lt; 1 are clamped to 1.</param>
    /// <param name="PageSize">Page size; clamped to <see cref="MaxPageSize"/>.</param>
    public sealed record Query(
        bool IncludeArchived,
        Guid? MemberId,
        string? Search,
        int Page,
        int PageSize)
        : IRequest<Result<HouseholdTaskListPageDto>>;

    /// <summary>Handler backed by EF Core.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<HouseholdTaskListPageDto>>
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
        public async Task<Result<HouseholdTaskListPageDto>> HandleAsync(
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

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                // ILIKE keeps the match case-insensitive at SQL level so the
                // CountAsync and the page query agree without any post-fetch
                // filtering. We trim before percent-wrapping so trailing
                // spaces from the search box don't kill the match.
                var pattern = $"%{request.Search.Trim()}%";
                query = query.Where(t =>
                    EF.Functions.ILike(t.Title, pattern)
                    || (t.Description != null && EF.Functions.ILike(t.Description, pattern))
                    || EF.Functions.ILike(t.Category, pattern));
            }

            var total = await query.CountAsync(cancellationToken);

            var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
            var page = Math.Max(1, request.Page);

            // ThenBy(Id) is the stable tie-breaker so two tasks created in the
            // same tick don't shuffle between pages on consecutive requests.
            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // For the "Todas" tab in the UI, attach the most recent completion of
            // each task — one row per task, the latest by completed_at. Cheap
            // single round trip with a window-by-task GROUP BY, restricted to
            // the page slice so search doesn't pull completions for filtered-
            // out tasks.
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

            IReadOnlyList<HouseholdTaskDto> items = [..
                tasks.Select(t => HouseholdTaskDto.From(
                    t,
                    latestByTask.TryGetValue(t.Id, out var c)
                        ? new LatestCompletionDto(c.OccurrenceDate, c.CompletedByMemberId, c.CompletedAt)
                        : null))];
            return new HouseholdTaskListPageDto(items, total, page, pageSize);
        }
    }
}

/// <summary>One page of the household tasks list.</summary>
/// <param name="Items">Tasks for the current page, newest first.</param>
/// <param name="Total">Total number of tasks after filters (used to compute page count).</param>
/// <param name="Page">1-based page index actually returned (clamped).</param>
/// <param name="PageSize">Page size actually used (clamped).</param>
public sealed record HouseholdTaskListPageDto(
    IReadOnlyList<HouseholdTaskDto> Items,
    int Total,
    int Page,
    int PageSize);
