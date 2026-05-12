using FamilyNido.Domain.School;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SchoolProfile"/>.</summary>
public sealed class SchoolProfileConfiguration : IEntityTypeConfiguration<SchoolProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchoolProfile> builder)
    {
        builder.ToTable("school_profiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.SchoolName).HasMaxLength(120);
        builder.Property(p => p.Grade).HasMaxLength(60);
        builder.Property(p => p.Tutor).HasMaxLength(120);
        builder.Property(p => p.Notes).HasMaxLength(2000);
        // TransportMode persisted as a readable string so the column self-documents.
        builder.Property(p => p.TransportMode).HasConversion<string>().HasMaxLength(8).IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(200);
        builder.Property(p => p.UpdatedBy).HasMaxLength(200);

        builder.HasOne(p => p.FamilyMember)
            .WithOne()
            .HasForeignKey<SchoolProfile>(p => p.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.FamilyMemberId).IsUnique();
    }
}
