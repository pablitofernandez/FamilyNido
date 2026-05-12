using FamilyNido.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Medication"/>.</summary>
public sealed class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.ToTable("medications");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).HasMaxLength(120).IsRequired();
        builder.Property(m => m.Dose).HasMaxLength(80);
        builder.Property(m => m.Frequency).HasMaxLength(120);
        builder.Property(m => m.Instructions).HasMaxLength(2000);

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).HasMaxLength(200);
        builder.Property(m => m.UpdatedBy).HasMaxLength(200);

        builder.HasOne(m => m.FamilyMember)
            .WithMany()
            .HasForeignKey(m => m.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.FamilyMemberId, m.StartDate });
    }
}
