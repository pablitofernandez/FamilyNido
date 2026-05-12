using FamilyNido.Domain.Wall;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="WallReaction"/>.</summary>
public sealed class WallReactionConfiguration : IEntityTypeConfiguration<WallReaction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WallReaction> builder)
    {
        builder.ToTable("wall_reactions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Emoji).HasMaxLength(16).IsRequired();
        builder.Property(r => r.ReactedAt).IsRequired();

        builder.HasOne(r => r.Member)
            .WithMany()
            .HasForeignKey(r => r.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // One reaction per (message, member, emoji). Idempotent add/remove uses this.
        builder.HasIndex(r => new { r.MessageId, r.MemberId, r.Emoji }).IsUnique();
    }
}
