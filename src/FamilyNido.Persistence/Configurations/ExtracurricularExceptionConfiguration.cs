using FamilyNido.Domain.School;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="ExtracurricularException"/>.</summary>
public sealed class ExtracurricularExceptionConfiguration : IEntityTypeConfiguration<ExtracurricularException>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ExtracurricularException> builder)
    {
        builder.ToTable("extracurricular_exceptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.UpdatedBy).HasMaxLength(200);

        builder.HasOne(x => x.DropoffMember)
            .WithMany()
            .HasForeignKey(x => x.DropoffMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PickupMember)
            .WithMany()
            .HasForeignKey(x => x.PickupMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        // At most one exception per (extracurricular, date).
        builder.HasIndex(x => new { x.ExtracurricularId, x.Date }).IsUnique();
    }
}
