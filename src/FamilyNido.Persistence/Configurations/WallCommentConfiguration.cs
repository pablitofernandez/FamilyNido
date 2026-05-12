using FamilyNido.Domain.Wall;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WallComment"/>.</summary>
public sealed class WallCommentConfiguration : IEntityTypeConfiguration<WallComment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WallComment> builder)
    {
        builder.ToTable("wall_comments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Text).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.TextHtml).HasMaxLength(4000).IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(200);
        builder.Property(c => c.UpdatedBy).HasMaxLength(200);

        builder.HasOne(c => c.AuthorMember)
            .WithMany()
            .HasForeignKey(c => c.AuthorMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.MessageId, c.CreatedAt });
    }
}
