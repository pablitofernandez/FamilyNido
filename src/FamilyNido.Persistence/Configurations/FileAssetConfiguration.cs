using FamilyNido.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="FileAsset"/>.</summary>
public sealed class FileAssetConfiguration : IEntityTypeConfiguration<FileAsset>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FileAsset> builder)
    {
        builder.ToTable("file_assets");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.RelativePath).HasMaxLength(400).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(80).IsRequired();
        builder.Property(a => a.SizeBytes).IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.CreatedBy).HasMaxLength(200);
        builder.Property(a => a.UpdatedBy).HasMaxLength(200);

        builder.HasOne(a => a.Family)
            .WithMany()
            .HasForeignKey(a => a.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.OwnerMember)
            .WithMany()
            .HasForeignKey(a => a.OwnerMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.RelativePath).IsUnique();
        builder.HasIndex(a => a.FamilyId);
    }
}
