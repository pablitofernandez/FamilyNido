using FamilyNido.Domain.Wall;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WallMessage"/>.</summary>
public sealed class WallMessageConfiguration : IEntityTypeConfiguration<WallMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WallMessage> builder)
    {
        builder.ToTable("wall_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Text).HasMaxLength(4000).IsRequired();
        builder.Property(m => m.TextHtml).HasMaxLength(8000).IsRequired();
        builder.Property(m => m.IsPinned).HasDefaultValue(false);

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).HasMaxLength(200);
        builder.Property(m => m.UpdatedBy).HasMaxLength(200);

        builder.HasOne(m => m.Family)
            .WithMany()
            .HasForeignKey(m => m.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AuthorMember)
            .WithMany()
            .HasForeignKey(m => m.AuthorMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // ImageFile nullable; set null on delete keeps the message.
        builder.HasOne(m => m.ImageFile)
            .WithMany()
            .HasForeignKey(m => m.ImageFileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(m => m.Comments)
            .WithOne(c => c.Message)
            .HasForeignKey(c => c.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Reactions)
            .WithOne(r => r.Message)
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.FamilyId, m.CreatedAt });
        builder.HasIndex(m => new { m.FamilyId, m.IsPinned, m.PinnedAt });
    }
}
