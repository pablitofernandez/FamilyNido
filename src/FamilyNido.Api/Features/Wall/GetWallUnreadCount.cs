using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>
/// Slice: count messages on the wall that post-date the caller's
/// <c>LastWallReadAt</c> watermark (RF-WALL-010). Messages authored by the caller
/// do not count — you cannot have unread mail from yourself.
/// </summary>
public static class GetWallUnreadCount
{
    /// <summary>Parameterless query — all inputs come from the authenticated user.</summary>
    public sealed record Query : IRequest<Result<UnreadCountDto>>;

    /// <summary>Response carrying the numeric count.</summary>
    /// <param name="Count">Unread messages for the caller.</param>
    public sealed record UnreadCountDto(int Count);

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<UnreadCountDto>>
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
        public async Task<Result<UnreadCountDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var watermark = current.User.LastWallReadAt ?? DateTimeOffset.MinValue;
            var myMemberId = current.Member.Id;

            var count = await _db.WallMessages
                .AsNoTracking()
                .Where(m => m.FamilyId == current.Family.Id
                    && m.CreatedAt > watermark
                    && m.AuthorMemberId != myMemberId)
                .CountAsync(cancellationToken);

            return new UnreadCountDto(count);
        }
    }
}
