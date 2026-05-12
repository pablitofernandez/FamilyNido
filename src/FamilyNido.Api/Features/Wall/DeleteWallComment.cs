using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Slice: delete a first-level reply. Only the author or an admin (RF-WALL-009 spirit).</summary>
public static class DeleteWallComment
{
    /// <summary>Command carrying the target comment id.</summary>
    public sealed record Command(Guid CommentId) : IRequest<Result<Unit>>;

    /// <summary>Handler.</summary>
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

            var comment = await _db.WallComments
                .Include(c => c.Message)
                .FirstOrDefaultAsync(
                    c => c.Id == request.CommentId && c.Message!.FamilyId == current.Family.Id,
                    cancellationToken);

            if (comment is null)
            {
                return ApplicationError.NotFound("wall_comment.not_found", $"Comment {request.CommentId} not found.");
            }

            var isAdmin = current.User.Role == FamilyRole.Admin;
            var isAuthor = current.Member.Id == comment.AuthorMemberId;
            if (!isAdmin && !isAuthor)
            {
                return ApplicationError.Forbidden(
                    "wall_comment.only_author_or_admin_can_delete",
                    "Only the author or a family admin may delete a comment.");
            }

            _db.WallComments.Remove(comment);
            await _db.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
