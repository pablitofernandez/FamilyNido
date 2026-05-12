using FamilyNido.Domain.Families;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Family"/>.</summary>
public sealed class FamilyConfiguration : IEntityTypeConfiguration<Family>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Family> builder)
    {
        builder.ToTable("families");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(120).IsRequired();
        builder.Property(f => f.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(f => f.Locale).HasMaxLength(16).IsRequired();
        builder.Property(f => f.LocationLabel).HasMaxLength(120);

        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.CreatedBy).HasMaxLength(200);
        builder.Property(f => f.UpdatedBy).HasMaxLength(200);

        builder.HasMany(f => f.Members)
            .WithOne(m => m.Family)
            .HasForeignKey(m => m.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
