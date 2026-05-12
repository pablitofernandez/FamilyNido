using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Slice: mark a wall message as pinned (RF-WALL-003). Any adult may pin.</summary>
public static class PinWallMessage
{
    /// <summary>Command carrying the target id.</summary>
    public sealed record Command(Guid MessageId) : IRequest<Result<WallMessageDto>>;

    /// <summary>Handler — idempotent pinning.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<WallMessageDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _clock;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            TimeProvider clock)
        {
            _db = db;
            _userContext = userContext;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<Result<WallMessageDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var message = await _db.WallMessages
                .Include(m => m.ImageFile)
                .Include(m => m.Comments)
                .Include(m => m.Reactions)
                .FirstOrDefaultAsync(
                    m => m.Id == request.MessageId && m.FamilyId == current.Family.Id,
                    cancellationToken);

            if (message is null)
            {
                return ApplicationError.NotFound("wall_message.not_found", $"Message {request.MessageId} not found.");
            }

            if (!message.IsPinned)
            {
                message.IsPinned = true;
                message.PinnedAt = _clock.GetUtcNow();
                await _db.SaveChangesAsync(cancellationToken);
            }

            return WallMessageDto.From(message);
        }
    }
}
