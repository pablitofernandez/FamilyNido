using FamilyNido.Domain.Families;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="FamilyMember"/>.</summary>
public sealed class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FamilyMember> builder)
    {
        builder.ToTable("family_members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(m => m.ColorHex).HasMaxLength(7).IsRequired();
        builder.Property(m => m.PhotoPath).HasMaxLength(260);
        builder.Property(m => m.ContactEmail).HasMaxLength(254);
        builder.Property(m => m.MemberType).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.IsActive).HasDefaultValue(true);

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).HasMaxLength(200);
        builder.Property(m => m.UpdatedBy).HasMaxLength(200);

        // One-to-one (optional) with User. FK lives on FamilyMember so we can have
        // many members without users (children, grandparents).
        builder.HasOne(m => m.User)
            .WithOne(u => u.FamilyMember)
            .HasForeignKey<FamilyMember>(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => new { m.FamilyId, m.IsActive });
        builder.HasIndex(m => m.UserId).IsUnique().HasFilter("user_id IS NOT NULL");
    }
}
