using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Slice: fetch a single wall message by id, scoped to the caller's family.</summary>
public static class GetWallMessage
{
    /// <summary>Query carrying the target id.</summary>
    public sealed record Query(Guid MessageId) : IRequest<Result<WallMessageDto>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<WallMessageDto>>
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
        public async Task<Result<WallMessageDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var message = await _db.WallMessages
                .AsNoTracking()
                .Include(m => m.ImageFile)
                .Include(m => m.Comments)
                .Include(m => m.Reactions)
                .FirstOrDefaultAsync(
                    m => m.Id == request.MessageId && m.FamilyId == current.Family.Id,
                    cancellationToken);

            return message is null
                ? ApplicationError.NotFound("wall_message.not_found", $"Message {request.MessageId} not found.")
                : WallMessageDto.From(message);
        }
    }
}
