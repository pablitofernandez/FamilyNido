using FamilyNido.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="LinkedCalendar"/>.</summary>
public sealed class LinkedCalendarConfiguration : IEntityTypeConfiguration<LinkedCalendar>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LinkedCalendar> builder)
    {
        builder.ToTable("linked_calendars");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ExternalCalendarId).HasMaxLength(320).IsRequired();
        builder.Property(c => c.Summary).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.ColorHex).HasMaxLength(7);
        builder.Property(c => c.IsImported).HasDefaultValue(false);

        // Sync tokens from Google can grow long; text rather than varchar.
        builder.Property(c => c.SyncToken).HasColumnType("text");
        builder.Property(c => c.LastSyncedAt);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(200);
        builder.Property(c => c.UpdatedBy).HasMaxLength(200);

        // FK to FamilyMember — set null so removing the member just unassigns the calendar.
        builder.HasOne(c => c.FamilyMember)
            .WithMany()
            .HasForeignKey(c => c.FamilyMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Events)
            .WithOne(e => e.LinkedCalendar)
            .HasForeignKey(e => e.LinkedCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        // A given Google calendar appears at most once per linked account.
        builder.HasIndex(c => new { c.GoogleAccountId, c.ExternalCalendarId }).IsUnique();
        builder.HasIndex(c => new { c.GoogleAccountId, c.IsImported });
    }
}
