using FamilyNido.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="EmailDigestRun"/>.</summary>
public sealed class EmailDigestRunConfiguration : IEntityTypeConfiguration<EmailDigestRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailDigestRun> builder)
    {
        builder.ToTable("email_digest_runs");

        // Composite PK is the deduplication contract — at most one row per (user, local-date).
        builder.HasKey(r => new { r.UserId, r.LocalDate });

        builder.Property(r => r.SentAt).IsRequired();

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
