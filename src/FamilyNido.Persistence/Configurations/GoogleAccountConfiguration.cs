using FamilyNido.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="GoogleAccount"/>.</summary>
public sealed class GoogleAccountConfiguration : IEntityTypeConfiguration<GoogleAccount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GoogleAccount> builder)
    {
        builder.ToTable("google_accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Email).HasMaxLength(320).IsRequired();
        builder.Property(a => a.DisplayName).HasMaxLength(200);

        // Refresh token ciphertext: short enough that a varchar fits, but text is safer
        // since Data Protection envelopes can grow (purpose strings, key rotation).
        builder.Property(a => a.EncryptedRefreshToken).HasColumnType("text").IsRequired();

        builder.Property(a => a.LastError).HasMaxLength(2000);
        builder.Property(a => a.IsRevoked).HasDefaultValue(false);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.CreatedBy).HasMaxLength(200);
        builder.Property(a => a.UpdatedBy).HasMaxLength(200);

        // FK to Family — restrict so a family with linked accounts cannot be deleted by accident.
        builder.HasOne(a => a.Family)
            .WithMany()
            .HasForeignKey(a => a.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to User — cascade so removing the user wipes their Google links and (transitively)
        // their cached events. Coherent with "users own their integrations".
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Calendars)
            .WithOne(c => c.GoogleAccount)
            .HasForeignKey(c => c.GoogleAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent the same user from linking the same Google account twice.
        builder.HasIndex(a => new { a.UserId, a.Email }).IsUnique();
        builder.HasIndex(a => a.FamilyId);
    }
}
