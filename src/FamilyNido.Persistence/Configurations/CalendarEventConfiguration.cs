using FamilyNido.Domain.Calendar;
using FamilyNido.Domain.Families;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="CalendarEvent"/>.</summary>
public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExternalEventId).HasMaxLength(1024).IsRequired();
        builder.Property(e => e.IcalUid).HasMaxLength(1024);
        builder.Property(e => e.Title).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.Location).HasMaxLength(500);
        builder.Property(e => e.OriginalTimeZone).HasMaxLength(64);
        builder.Property(e => e.HtmlLink).HasMaxLength(1024);

        builder.Property(e => e.StartAt).IsRequired();
        builder.Property(e => e.EndAt).IsRequired();
        builder.Property(e => e.IsAllDay).HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(200);
        builder.Property(e => e.UpdatedBy).HasMaxLength(200);

        // FK to Family — restrict so we never orphan events at family deletion time.
        builder.HasOne(e => e.Family)
            .WithMany()
            .HasForeignKey(e => e.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique per (calendar, external id) — upsert key for the sync engine.
        builder.HasIndex(e => new { e.LinkedCalendarId, e.ExternalEventId }).IsUnique();

        // Range queries by family + time window — the dominant access pattern.
        builder.HasIndex(e => new { e.FamilyId, e.StartAt });
        builder.HasIndex(e => new { e.FamilyId, e.EndAt });

        // M:N with FamilyMember for "this event concerns these people". Cascade on
        // both ends: if an event is removed (Google cancellation) the relations
        // disappear; if a member is deleted the relations follow them out.
        builder.HasMany(e => e.RelatedMembers)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "calendar_event_members",
                right => right.HasOne<FamilyMember>()
                    .WithMany()
                    .HasForeignKey("family_member_id")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<CalendarEvent>()
                    .WithMany()
                    .HasForeignKey("calendar_event_id")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("calendar_event_members");
                    join.HasKey("calendar_event_id", "family_member_id");
                });
    }
}
