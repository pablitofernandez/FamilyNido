namespace FamilyNido.Api.Features.Wall;

/// <summary>
/// Aggregated reaction bucket for a specific emoji on a message. Carries the count
/// plus the ids of members who reacted, so the UI can render "Dan, Ana + 3 más".
/// </summary>
/// <param name="Emoji">The emoji string (e.g. <c>"❤️"</c>).</param>
/// <param name="Count">Number of distinct members who reacted with this emoji.</param>
/// <param name="MemberIds">Ids of the reacting members, in no particular order.</param>
public sealed record WallReactionSummaryDto(
    string Emoji,
    int Count,
    IReadOnlyList<Guid> MemberIds);
