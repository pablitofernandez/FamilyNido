using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Wall;

/// <summary>
/// A 1-level reply under a <see cref="WallMessage"/>. Threading is intentionally flat:
/// the module is a family "pizarra", not Reddit — deeper trees would obscure more than
/// they'd help.
/// </summary>
public sealed class WallComment : AuditableEntity
{
    /// <summary>Owning message.</summary>
    public required Guid MessageId { get; set; }

    /// <summary>Navigation to the owning <see cref="WallMessage"/>.</summary>
    public WallMessage? Message { get; set; }

    /// <summary>Member who authored the comment.</summary>
    public required Guid AuthorMemberId { get; set; }

    /// <summary>Navigation to the comment author.</summary>
    public FamilyMember? AuthorMember { get; set; }

    /// <summary>Raw markdown source as typed by the user.</summary>
    public required string Text { get; set; }

    /// <summary>Pre-rendered, sanitized HTML derived from <see cref="Text"/>.</summary>
    public required string TextHtml { get; set; }

    /// <summary>Members referenced via <c>@DisplayName</c> in <see cref="Text"/> — drives notifications.</summary>
    public ICollection<WallCommentMention> Mentions { get; set; } = [];
}
