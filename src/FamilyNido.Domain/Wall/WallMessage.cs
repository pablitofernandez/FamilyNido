using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Files;

namespace FamilyNido.Domain.Wall;

/// <summary>
/// A post on the family wall. Carries markdown source plus pre-rendered HTML
/// (sanitized at write time with Markdig) and optionally a single attached image.
/// Messages can be pinned, reacted to and commented on. Totally internal to the
/// family — never shared outside.
/// </summary>
public sealed class WallMessage : AuditableEntity
{
    /// <summary>Family this message belongs to (authorization boundary).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Member who authored the message.</summary>
    public required Guid AuthorMemberId { get; set; }

    /// <summary>Navigation to the author.</summary>
    public FamilyMember? AuthorMember { get; set; }

    /// <summary>Raw markdown source as typed by the user. Preserved so edits work on the original.</summary>
    public required string Text { get; set; }

    /// <summary>Pre-rendered, sanitized HTML derived from <see cref="Text"/> via Markdig.</summary>
    public required string TextHtml { get; set; }

    /// <summary>
    /// Optional image attached to the message. Nullable because most messages are text-only.
    /// Deleting the file unsets this FK (SetNull) so the message is not lost with it.
    /// </summary>
    public Guid? ImageFileId { get; set; }

    /// <summary>Navigation to the attached <see cref="FileAsset"/>.</summary>
    public FileAsset? ImageFile { get; set; }

    /// <summary>When true, the message is shown in the pinned zone at the top of the wall.</summary>
    public bool IsPinned { get; set; }

    /// <summary>UTC instant the message was pinned; null when not pinned.</summary>
    public DateTimeOffset? PinnedAt { get; set; }

    /// <summary>1-level thread of replies below the message (RF-WALL-005).</summary>
    public ICollection<WallComment> Comments { get; set; } = [];

    /// <summary>Emoji reactions — one row per <c>(member, emoji)</c> on this message.</summary>
    public ICollection<WallReaction> Reactions { get; set; } = [];

    /// <summary>Members referenced via <c>@DisplayName</c> in <see cref="Text"/> — drives notifications.</summary>
    public ICollection<WallMessageMention> Mentions { get; set; } = [];
}
