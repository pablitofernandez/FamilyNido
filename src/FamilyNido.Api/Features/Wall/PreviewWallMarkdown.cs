using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Markdown;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>
/// Slice for <c>POST /api/wall/preview</c>. Renders raw markdown using the same
/// pipeline that <see cref="CreateWallMessage"/> uses on save, so the composer
/// can show a faithful WYSIWYG preview without re-implementing markdown on the
/// client. Mentions are resolved against the caller's family roster.
/// </summary>
public static class PreviewWallMarkdown
{
    /// <summary>Command carrying the raw markdown text.</summary>
    public sealed record Command(string Text) : IRequest<Result<PreviewDto>>;

    /// <summary>Wire shape — only the rendered HTML is useful to the client.</summary>
    public sealed record PreviewDto(string Html);

    /// <summary>Input validation — caps the size to match the create slice.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with the size rule.</summary>
        public Validator()
        {
            RuleFor(x => x.Text).MaximumLength(4000);
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<PreviewDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly MarkdownRenderer _markdown;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            MarkdownRenderer markdown)
        {
            _db = db;
            _userContext = userContext;
            _markdown = markdown;
        }

        /// <inheritdoc />
        public async Task<Result<PreviewDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            // Empty text → empty preview, no DB hit needed.
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return new PreviewDto(string.Empty);
            }

            var candidates = await _db.FamilyMembers
                .AsNoTracking()
                .Where(m => m.FamilyId == current.Family.Id && m.IsActive)
                .Select(m => new MentionCandidate(m.Id, m.DisplayName))
                .ToListAsync(cancellationToken);

            var rendered = _markdown.RenderWithMentions(request.Text, candidates);
            return new PreviewDto(rendered.Html);
        }
    }
}
