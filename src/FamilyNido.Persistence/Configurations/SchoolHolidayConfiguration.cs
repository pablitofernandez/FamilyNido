using FamilyNido.Domain.School;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SchoolHoliday"/>.</summary>
public sealed class SchoolHolidayConfiguration : IEntityTypeConfiguration<SchoolHoliday>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchoolHoliday> builder)
    {
        builder.ToTable("school_holidays");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Label).HasMaxLength(120).IsRequired();

        builder.Property(h => h.CreatedAt).IsRequired();
        builder.Property(h => h.CreatedBy).HasMaxLength(200);
        builder.Property(h => h.UpdatedBy).HasMaxLength(200);

        builder.HasOne(h => h.Family)
            .WithMany()
            .HasForeignKey(h => h.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => new { h.FamilyId, h.StartDate });
    }
}
