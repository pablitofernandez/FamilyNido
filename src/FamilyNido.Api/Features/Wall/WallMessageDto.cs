using FamilyNido.Api.Features.Files;
using FamilyNido.Domain.Wall;

namespace FamilyNido.Api.Features.Wall;

/// <summary>Read-model projection of a <see cref="WallMessage"/> returned by the API.</summary>
/// <param name="Id">Stable message identifier.</param>
/// <param name="AuthorMemberId">Author (family member).</param>
/// <param name="Text">Raw markdown source (kept for edits).</param>
/// <param name="TextHtml">Pre-rendered sanitized HTML to show to the user.</param>
/// <param name="Image">Attached image metadata with loadable URL, if any.</param>
/// <param name="IsPinned">Whether the message is in the pinned zone.</param>
/// <param name="PinnedAt">When the message was pinned, if any.</param>
/// <param name="CreatedAt">UTC instant of creation.</param>
/// <param name="Comments">First-level replies, newest first.</param>
/// <param name="Reactions">Aggregated reactions (emoji + count + members).</param>
public sealed record WallMessageDto(
    Guid Id,
    Guid AuthorMemberId,
    string Text,
    string TextHtml,
    FileAssetDto? Image,
    bool IsPinned,
    DateTimeOffset? PinnedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<WallCommentDto> Comments,
    IReadOnlyList<WallReactionSummaryDto> Reactions)
{
    /// <summary>Project a loaded domain entity to the DTO shape used by the API.</summary>
    public static WallMessageDto From(WallMessage m) => new(
        Id: m.Id,
        AuthorMemberId: m.AuthorMemberId,
        Text: m.Text,
        TextHtml: m.TextHtml,
        Image: m.ImageFile is null ? null : FileAssetDto.From(m.ImageFile),
        IsPinned: m.IsPinned,
        PinnedAt: m.PinnedAt,
        CreatedAt: m.CreatedAt,
        Comments: [.. m.Comments.OrderBy(c => c.CreatedAt).Select(WallCommentDto.From)],
        Reactions: [.. m.Reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new WallReactionSummaryDto(
                Emoji: g.Key,
                Count: g.Count(),
                MemberIds: [.. g.Select(r => r.MemberId)]))
            .OrderByDescending(s => s.Count)]);
}
