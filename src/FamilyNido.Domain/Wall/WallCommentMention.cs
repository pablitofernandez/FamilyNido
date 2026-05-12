using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Wall;

/// <summary>
/// Join row linking a <see cref="WallComment"/> with a <see cref="FamilyMember"/>
/// referenced via <c>@DisplayName</c>. Mirror of <see cref="WallMessageMention"/>
/// for thread replies.
/// </summary>
public sealed class WallCommentMention
{
    /// <summary>Mentioned-in comment.</summary>
    public required Guid CommentId { get; set; }

    /// <summary>Navigation to the owning <see cref="WallComment"/>.</summary>
    public WallComment? Comment { get; set; }

    /// <summary>Mentioned member.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the mentioned <see cref="FamilyMember"/>.</summary>
    public FamilyMember? FamilyMember { get; set; }
}
