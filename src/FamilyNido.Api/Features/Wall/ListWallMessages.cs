using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>
/// Slice: list wall messages for the caller's family (RF-WALL-001..006). Cursor-based
/// over <c>CreatedAt</c> descending. Pinned messages live in a separate bucket so the
/// UI can render them above the feed without interleaving.
/// </summary>
public static class ListWallMessages
{
    /// <summary>Page size default when the caller does not specify one.</summary>
    public const int DefaultLimit = 20;

    /// <summary>Maximum page size accepted (anything higher is clamped).</summary>
    public const int MaxLimit = 50;

    /// <summary>Query carrying the cursor.</summary>
    /// <param name="Before">Cursor: return only messages strictly older than this instant.</param>
    /// <param name="Limit">Page size; clamped to <see cref="MaxLimit"/>.</param>
    public sealed record Query(DateTimeOffset? Before, int? Limit) : IRequest<Result<WallFeedPageDto>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<WallFeedPageDto>>
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
        public async Task<Result<WallFeedPageDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);

            var pinned = await _db.WallMessages
                .AsNoTracking()
                .Include(m => m.ImageFile)
                .Include(m => m.Comments)
                .Include(m => m.Reactions)
                .Where(m => m.FamilyId == current.Family.Id && m.IsPinned)
                .OrderByDescending(m => m.PinnedAt)
                .ToListAsync(cancellationToken);

            var feedQuery = _db.WallMessages
                .AsNoTracking()
                .Include(m => m.ImageFile)
                .Include(m => m.Comments)
                .Include(m => m.Reactions)
                .Where(m => m.FamilyId == current.Family.Id && !m.IsPinned);

            if (request.Before is { } before)
            {
                feedQuery = feedQuery.Where(m => m.CreatedAt < before);
            }

            // Fetch one extra row to know cheaply whether more pages exist.
            var fetched = await feedQuery
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);

            var hasMore = fetched.Count > limit;
            var page = fetched.Take(limit).ToList();

            return new WallFeedPageDto(
                Pinned: [.. pinned.Select(WallMessageDto.From)],
                Messages: [.. page.Select(WallMessageDto.From)],
                HasMore: hasMore);
        }
    }
}

/// <summary>One page of the wall feed: the full pinned list plus a cursor-paginated message window.</summary>
/// <param name="Pinned">Always-full list of pinned messages (typically few).</param>
/// <param name="Messages">Non-pinned messages newest first.</param>
/// <param name="HasMore">True when another page is available.</param>
public sealed record WallFeedPageDto(
    IReadOnlyList<WallMessageDto> Pinned,
    IReadOnlyList<WallMessageDto> Messages,
    bool HasMore);
