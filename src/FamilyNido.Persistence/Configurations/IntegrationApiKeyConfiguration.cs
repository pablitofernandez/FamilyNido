using FamilyNido.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="IntegrationApiKey"/>.</summary>
public sealed class IntegrationApiKeyConfiguration : IEntityTypeConfiguration<IntegrationApiKey>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IntegrationApiKey> builder)
    {
        builder.ToTable("integration_api_keys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Name).IsRequired().HasMaxLength(80);
        // SHA-256 hex digest is exactly 64 chars; clamp to that to keep the
        // index entry tight and to make a malformed value visually obvious.
        builder.Property(k => k.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(k => k.Prefix).IsRequired().HasMaxLength(16);

        // Unique on the hash because that is the lookup key on every request.
        builder.HasIndex(k => k.TokenHash).IsUnique();
        builder.HasIndex(k => k.FamilyId);

        builder.HasOne(k => k.Family)
            .WithMany()
            .HasForeignKey(k => k.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict so deleting the author member surfaces the conflict instead
        // of silently leaving a token attributed to a non-existent person.
        builder.HasOne(k => k.AuthorMember)
            .WithMany()
            .HasForeignKey(k => k.AuthorMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
