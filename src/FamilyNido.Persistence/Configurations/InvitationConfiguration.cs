using FamilyNido.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="Invitation"/>.</summary>
public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).HasMaxLength(254).IsRequired();
        builder.Property(i => i.RoleOnAccept).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.TokenHash).HasColumnType("bytea").IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.CreatedBy).HasMaxLength(200);
        builder.Property(i => i.UpdatedBy).HasMaxLength(200);

        // Token lookup is the hot path during /accept — UNIQUE both for safety
        // and to enable index-only lookups by hash.
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // Listing pending invitations of a family is a common admin query; the
        // partial index keeps it tiny since most rows eventually move to
        // consumed/revoked state.
        builder.HasIndex(i => new { i.FamilyId, i.ConsumedAt, i.RevokedAt });

        builder.HasIndex(i => i.FamilyMemberId);

        // FK to FamilyMember without cascade: an admin must revoke pending
        // invitations explicitly before deleting the member. Prevents
        // accidentally orphaning rows mid-flow.
        builder.HasOne(i => i.FamilyMember)
            .WithMany()
            .HasForeignKey(i => i.FamilyMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
