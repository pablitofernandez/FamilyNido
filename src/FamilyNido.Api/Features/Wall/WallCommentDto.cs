using FamilyNido.Domain.Wall;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Read-model projection of a <see cref="WallComment"/> returned by the API.</summary>
/// <param name="Id">Stable comment identifier.</param>
/// <param name="MessageId">Message this comment belongs to.</param>
/// <param name="AuthorMemberId">Author (family member).</param>
/// <param name="Text">Raw markdown source.</param>
/// <param name="TextHtml">Pre-rendered sanitized HTML.</param>
/// <param name="CreatedAt">UTC instant of creation.</param>
public sealed record WallCommentDto(
    Guid Id,
    Guid MessageId,
    Guid AuthorMemberId,
    string Text,
    string TextHtml,
    DateTimeOffset CreatedAt)
{
    /// <summary>Project a domain entity to the DTO shape used by the API.</summary>
    public static WallCommentDto From(WallComment c) => new(
        Id: c.Id,
        MessageId: c.MessageId,
        AuthorMemberId: c.AuthorMemberId,
        Text: c.Text,
        TextHtml: c.TextHtml,
        CreatedAt: c.CreatedAt);
}
