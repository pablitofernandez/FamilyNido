using FamilyNido.Domain.School;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SchoolDaySchedule"/>.</summary>
public sealed class SchoolDayScheduleConfiguration : IEntityTypeConfiguration<SchoolDaySchedule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchoolDaySchedule> builder)
    {
        builder.ToTable("school_day_schedules");

        // Composite PK: at most one row per (kid, weekday).
        builder.HasKey(s => new { s.FamilyMemberId, s.DayOfWeek });

        builder.Property(s => s.DayOfWeek).HasConversion<int>();

        builder.HasOne(s => s.FamilyMember)
            .WithMany()
            .HasForeignKey(s => s.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.DropoffMember)
            .WithMany()
            .HasForeignKey(s => s.DropoffMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.PickupMember)
            .WithMany()
            .HasForeignKey(s => s.PickupMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
