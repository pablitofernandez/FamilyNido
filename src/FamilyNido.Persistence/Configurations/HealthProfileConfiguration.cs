using FamilyNido.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="HealthProfile"/>.</summary>
public sealed class HealthProfileConfiguration : IEntityTypeConfiguration<HealthProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HealthProfile> builder)
    {
        builder.ToTable("health_profiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.BloodType).HasMaxLength(8);
        builder.Property(p => p.Allergies).HasMaxLength(2000);
        builder.Property(p => p.ChronicConditions).HasMaxLength(2000);
        builder.Property(p => p.Notes).HasMaxLength(4000);

        // Audit columns for AuditableEntity.
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(200);
        builder.Property(p => p.UpdatedBy).HasMaxLength(200);

        // 1:1 with the member — unique index doubles as the lookup key.
        builder.HasOne(p => p.FamilyMember)
            .WithOne()
            .HasForeignKey<HealthProfile>(p => p.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.FamilyMemberId).IsUnique();
    }
}
