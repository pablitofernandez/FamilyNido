using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Slice: hard-delete a wall message. Only the author or an admin (RF-WALL-009).</summary>
public static class DeleteWallMessage
{
    /// <summary>Command carrying the target id.</summary>
    public sealed record Command(Guid MessageId) : IRequest<Result<Unit>>;

    /// <summary>Handler — enforces author-or-admin rule.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
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
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken cancellationToken)
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

            var isAdmin = current.User.Role == FamilyRole.Admin;
            var isAuthor = current.Member.Id == message.AuthorMemberId;
            if (!isAdmin && !isAuthor)
            {
                return ApplicationError.Forbidden(
                    "wall_message.only_author_or_admin_can_delete",
                    "Only the author or a family admin may delete a message.");
            }

            _db.WallMessages.Remove(message);
            await _db.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
