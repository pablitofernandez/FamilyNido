using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Wall;

/// <summary>
/// Join row linking a <see cref="WallMessage"/> with a <see cref="FamilyMember"/>
/// referenced via <c>@DisplayName</c> in the markdown source. Drives mention
/// notifications and visual highlights — never carries content of its own.
/// </summary>
public sealed class WallMessageMention
{
    /// <summary>Mentioned-in message.</summary>
    public required Guid MessageId { get; set; }

    /// <summary>Navigation to the owning <see cref="WallMessage"/>.</summary>
    public WallMessage? Message { get; set; }

    /// <summary>Mentioned member.</summary>
    public required Guid FamilyMemberId { get; set; }

    /// <summary>Navigation to the mentioned <see cref="FamilyMember"/>.</summary>
    public FamilyMember? FamilyMember { get; set; }
}
