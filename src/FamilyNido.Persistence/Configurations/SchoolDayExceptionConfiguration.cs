using FamilyNido.Domain.School;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SchoolDayException"/>.</summary>
public sealed class SchoolDayExceptionConfiguration : IEntityTypeConfiguration<SchoolDayException>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchoolDayException> builder)
    {
        builder.ToTable("school_day_exceptions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.MorningTime);
        builder.Property(e => e.AfternoonTime);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.UpdatedBy).HasMaxLength(200);

        builder.HasOne(e => e.FamilyMember)
            .WithMany()
            .HasForeignKey(e => e.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.DropoffMember)
            .WithMany()
            .HasForeignKey(e => e.DropoffMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.PickupMember)
            .WithMany()
            .HasForeignKey(e => e.PickupMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.FamilyMemberId, e.Date }).IsUnique();
        builder.HasIndex(e => new { e.FamilyId, e.Date });
    }
}
