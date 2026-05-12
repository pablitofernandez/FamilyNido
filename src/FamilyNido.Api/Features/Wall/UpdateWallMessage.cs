using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Features.Notifications;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Markdown;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Wall;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Slice: edit an existing wall message. Only the author or an admin may edit (RF-WALL-009 spirit).</summary>
public static class UpdateWallMessage
{
    /// <summary>Command carrying the id + new payload.</summary>
    public sealed record Command(
        Guid MessageId,
        string Text,
        Guid? ImageFileId) : IRequest<Result<WallMessageDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.MessageId).NotEmpty();
            RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
        }
    }

    /// <summary>Handler — updates and re-renders.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<WallMessageDto>>
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
                .Include(m => m.Mentions)
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
                    "wall_message.only_author_or_admin_can_edit",
                    "Only the author or a family admin may edit a message.");
            }

            if (request.ImageFileId is { } imageId && imageId != message.ImageFileId)
            {
                var imageOk = await _db.FileAssets
                    .AnyAsync(a => a.Id == imageId && a.FamilyId == current.Family.Id, cancellationToken);
                if (!imageOk)
                {
                    return ApplicationError.Validation(
                        "wall_message.invalid_image",
                        "Attached image does not belong to this family.");
                }
            }

            var candidates = await _db.FamilyMembers
                .AsNoTracking()
                .Where(m => m.FamilyId == current.Family.Id && m.IsActive)
                .Select(m => new MentionCandidate(m.Id, m.DisplayName))
                .ToListAsync(cancellationToken);

            var rendered = _markdown.RenderWithMentions(request.Text, candidates);
            message.Text = rendered.Markdown;
            message.TextHtml = rendered.Html;
            message.ImageFileId = request.ImageFileId;

            // Capture the previous mention set so we can notify only newcomers —
            // editing a message should not re-spam people who were already pinged.
            var previousMentionIds = message.Mentions.Select(x => x.FamilyMemberId).ToHashSet();

            // Replace the mention set entirely — easier than diffing add/remove.
            message.Mentions.Clear();
            foreach (var memberId in rendered.MentionedMemberIds)
            {
                message.Mentions.Add(new WallMessageMention { MessageId = message.Id, FamilyMemberId = memberId });
            }

            await _db.SaveChangesAsync(cancellationToken);

            var newlyMentioned = rendered.MentionedMemberIds
                .Where(id => !previousMentionIds.Contains(id))
                .ToList();
            if (newlyMentioned.Count > 0)
            {
                var snippet = EmailTemplates.MakeSnippet(rendered.Markdown);
                await _notifications.NotifyWallMentionAsync(
                    newlyMentioned,
                    current.Member.Id,
                    "message",
                    snippet,
                    cancellationToken);
            }

            // Reload to refresh ImageFile navigation with new FK.
            var updated = await _db.WallMessages
                .AsNoTracking()
                .Include(m => m.ImageFile)
                .Include(m => m.Comments)
                .Include(m => m.Reactions)
                .FirstAsync(m => m.Id == message.Id, cancellationToken);

            var dto = WallMessageDto.From(updated);

            return dto;
        }
    }
}
