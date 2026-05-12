using FamilyNido.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Vaccination"/>.</summary>
public sealed class VaccinationConfiguration : IEntityTypeConfiguration<Vaccination>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Vaccination> builder)
    {
        builder.ToTable("vaccinations");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).HasMaxLength(120).IsRequired();
        builder.Property(v => v.Notes).HasMaxLength(2000);

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.CreatedBy).HasMaxLength(200);
        builder.Property(v => v.UpdatedBy).HasMaxLength(200);

        builder.HasOne(v => v.FamilyMember)
            .WithMany()
            .HasForeignKey(v => v.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.FamilyMemberId, v.Date });
    }
}
