using FamilyNido.Domain.Wall;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WallMessageMention"/>.</summary>
public sealed class WallMessageMentionConfiguration : IEntityTypeConfiguration<WallMessageMention>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WallMessageMention> builder)
    {
        builder.ToTable("wall_message_mentions");

        // Composite PK enforces uniqueness per (message, member) — a single
        // member can only be mentioned once per message regardless of repeats.
        builder.HasKey(x => new { x.MessageId, x.FamilyMemberId });

        builder.HasOne(x => x.Message)
            .WithMany(m => m.Mentions)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FamilyMember)
            .WithMany()
            .HasForeignKey(x => x.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Reverse lookup "messages I'm mentioned in".
        builder.HasIndex(x => x.FamilyMemberId);
    }
}
