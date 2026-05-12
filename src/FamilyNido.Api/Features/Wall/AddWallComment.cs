using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Features.Notifications;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Markdown;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Wall;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Slice: add a first-level reply to a wall message (RF-WALL-005).</summary>
public static class AddWallComment
{
    /// <summary>Command carrying the target message + text.</summary>
    public sealed record Command(Guid MessageId, string Text) : IRequest<Result<WallCommentDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.MessageId).NotEmpty();
            RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        }
    }

    /// <summary>Handler — renders markdown and persists.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<WallCommentDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly MarkdownRenderer _markdown;
        private readonly NotificationService _notifications;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            MarkdownRenderer markdown,
            NotificationService notifications)
        {
            _db = db;
            _userContext = userContext;
            _markdown = markdown;
            _notifications = notifications;
        }

        /// <inheritdoc />
        public async Task<Result<WallCommentDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var messageExists = await _db.WallMessages
                .AnyAsync(m => m.Id == request.MessageId && m.FamilyId == current.Family.Id, cancellationToken);
            if (!messageExists)
            {
                return ApplicationError.NotFound("wall_message.not_found", $"Message {request.MessageId} not found.");
            }

            var candidates = await _db.FamilyMembers
                .AsNoTracking()
                .Where(m => m.FamilyId == current.Family.Id && m.IsActive)
                .Select(m => new MentionCandidate(m.Id, m.DisplayName))
                .ToListAsync(cancellationToken);

            var rendered = _markdown.RenderWithMentions(request.Text, candidates);
            if (string.IsNullOrEmpty(rendered.Markdown))
            {
                return ApplicationError.Validation("wall_comment.empty_text", "Comment text cannot be empty.");
            }

            var comment = new WallComment
            {
                MessageId = request.MessageId,
                AuthorMemberId = current.Member.Id,
                Text = rendered.Markdown,
                TextHtml = rendered.Html,
            };

            foreach (var memberId in rendered.MentionedMemberIds)
            {
                comment.Mentions.Add(new WallCommentMention { CommentId = comment.Id, FamilyMemberId = memberId });
            }

            _db.WallComments.Add(comment);
            await _db.SaveChangesAsync(cancellationToken);

            var dto = WallCommentDto.From(comment);

            if (rendered.MentionedMemberIds.Count > 0)
            {
                var snippet = EmailTemplates.MakeSnippet(rendered.Markdown);
                await _notifications.NotifyWallMentionAsync(
                    rendered.MentionedMemberIds,
                    current.Member.Id,
                    "comment",
                    snippet,
                    cancellationToken);
            }

            return dto;
        }
    }
}
