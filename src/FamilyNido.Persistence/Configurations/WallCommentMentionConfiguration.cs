using FamilyNido.Domain.Wall;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WallCommentMention"/>.</summary>
public sealed class WallCommentMentionConfiguration : IEntityTypeConfiguration<WallCommentMention>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WallCommentMention> builder)
    {
        builder.ToTable("wall_comment_mentions");

        builder.HasKey(x => new { x.CommentId, x.FamilyMemberId });

        builder.HasOne(x => x.Comment)
            .WithMany(c => c.Mentions)
            .HasForeignKey(x => x.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FamilyMember)
            .WithMany()
            .HasForeignKey(x => x.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.FamilyMemberId);
    }
}
