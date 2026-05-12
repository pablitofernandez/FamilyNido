using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Wall;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>
/// Slice: add or remove a single emoji reaction from the caller on a wall message
/// (RF-WALL-004). Called the same way in both directions — the handler checks the
/// existing state and flips it.
/// </summary>
public static class ToggleWallReaction
{
    /// <summary>Command carrying the target message + emoji.</summary>
    public sealed record Command(Guid MessageId, string Emoji) : IRequest<Result<ToggleResultDto>>;

    /// <summary>Result after toggling: reports the final direction + the updated summary bucket.</summary>
    /// <param name="MessageId">Message affected.</param>
    /// <param name="Emoji">Emoji toggled.</param>
    /// <param name="IsReacted">True if the caller now has a reaction, false if it was retired.</param>
    /// <param name="Summary">Aggregated bucket for this emoji after the toggle.</param>
    public sealed record ToggleResultDto(
        Guid MessageId,
        string Emoji,
        bool IsReacted,
        WallReactionSummaryDto Summary);

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.MessageId).NotEmpty();
            RuleFor(x => x.Emoji).NotEmpty().MaximumLength(16);
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<ToggleResultDto>>
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
        public async Task<Result<ToggleResultDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var message = await _db.WallMessages
                .FirstOrDefaultAsync(
                    m => m.Id == request.MessageId && m.FamilyId == current.Family.Id,
                    cancellationToken);
            if (message is null)
            {
                return ApplicationError.NotFound("wall_message.not_found", $"Message {request.MessageId} not found.");
            }

            var existing = await _db.WallReactions
                .FirstOrDefaultAsync(
                    r => r.MessageId == request.MessageId
                         && r.MemberId == current.Member.Id
                         && r.Emoji == request.Emoji,
                    cancellationToken);

            bool isReacted;
            if (existing is not null)
            {
                _db.WallReactions.Remove(existing);
                isReacted = false;
            }
            else
            {
                _db.WallReactions.Add(new WallReaction
                {
                    MessageId = request.MessageId,
                    MemberId = current.Member.Id,
                    Emoji = request.Emoji,
                    ReactedAt = _clock.GetUtcNow(),
                });
                isReacted = true;
            }

            await _db.SaveChangesAsync(cancellationToken);

            // Rebuild the summary bucket for this emoji after the mutation.
            var remaining = await _db.WallReactions
                .AsNoTracking()
                .Where(r => r.MessageId == request.MessageId && r.Emoji == request.Emoji)
                .Select(r => r.MemberId)
                .ToListAsync(cancellationToken);

            var summary = new WallReactionSummaryDto(
                Emoji: request.Emoji,
                Count: remaining.Count,
                MemberIds: remaining);

            return new ToggleResultDto(
                MessageId: request.MessageId,
                Emoji: request.Emoji,
                IsReacted: isReacted,
                Summary: summary);
        }
    }
}
