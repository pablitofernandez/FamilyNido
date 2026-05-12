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

/// <summary>Slice: publish a new wall message (RF-WALL-001). Renders markdown.</summary>
public static class CreateWallMessage
{
    /// <summary>Command carrying the payload.</summary>
    /// <param name="Text">Raw markdown. 1..4000 chars.</param>
    /// <param name="ImageFileId">Optional image attachment (must belong to the caller's family).</param>
    public sealed record Command(string Text, Guid? ImageFileId)
        : IRequest<Result<WallMessageDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
        }
    }

    /// <summary>Handler — persists the message and renders markdown.</summary>
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

            if (request.ImageFileId is { } imageId)
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

            // Active members of the caller's family are the only mention candidates —
            // pulling them once keeps the renderer pure and avoids a DB hit for each match.
            var candidates = await _db.FamilyMembers
                .AsNoTracking()
                .Where(m => m.FamilyId == current.Family.Id && m.IsActive)
                .Select(m => new MentionCandidate(m.Id, m.DisplayName))
                .ToListAsync(cancellationToken);

            var rendered = _markdown.RenderWithMentions(request.Text, candidates);
            if (string.IsNullOrEmpty(rendered.Markdown))
            {
                return ApplicationError.Validation("wall_message.empty_text", "Message text cannot be empty.");
            }

            var message = new WallMessage
            {
                FamilyId = current.Family.Id,
                AuthorMemberId = current.Member.Id,
                Text = rendered.Markdown,
                TextHtml = rendered.Html,
                ImageFileId = request.ImageFileId,
            };

            foreach (var memberId in rendered.MentionedMemberIds)
            {
                message.Mentions.Add(new WallMessageMention { MessageId = message.Id, FamilyMemberId = memberId });
            }

            _db.WallMessages.Add(message);
            await _db.SaveChangesAsync(cancellationToken);

            // Reload with Include so the DTO carries image + collections (empty here).
            var created = await _db.WallMessages
                .AsNoTracking()
                .Include(m => m.ImageFile)
                .Include(m => m.Comments)
                .Include(m => m.Reactions)
                .FirstAsync(m => m.Id == message.Id, cancellationToken);

            var dto = WallMessageDto.From(created);

            if (rendered.MentionedMemberIds.Count > 0)
            {
                var snippet = EmailTemplates.MakeSnippet(rendered.Markdown);
                await _notifications.NotifyWallMentionAsync(
                    rendered.MentionedMemberIds,
                    current.Member.Id,
                    "message",
                    snippet,
                    cancellationToken);
            }

            return dto;
        }
    }
}
