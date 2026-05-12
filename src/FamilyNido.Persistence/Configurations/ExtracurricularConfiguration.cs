using FamilyNido.Domain.School;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Extracurricular"/>.</summary>
public sealed class ExtracurricularConfiguration : IEntityTypeConfiguration<Extracurricular>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Extracurricular> builder)
    {
        builder.ToTable("extracurriculars");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Location).HasMaxLength(160);
        builder.Property(e => e.ContactPhone).HasMaxLength(40);
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.IsArchived).HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.UpdatedBy).HasMaxLength(200);

        builder.HasOne(e => e.FamilyMember)
            .WithMany()
            .HasForeignKey(e => e.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.DefaultDropoffMember)
            .WithMany()
            .HasForeignKey(e => e.DefaultDropoffMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.DefaultPickupMember)
            .WithMany()
            .HasForeignKey(e => e.DefaultPickupMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Exceptions)
            .WithOne(x => x.Extracurricular)
            .HasForeignKey(x => x.ExtracurricularId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.FamilyId, e.IsArchived });
    }
}
