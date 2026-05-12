using FamilyNido.Domain.Families;
using FamilyNido.Domain.HouseholdTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="HouseholdTask"/>.</summary>
public sealed class HouseholdTaskConfiguration : IEntityTypeConfiguration<HouseholdTask>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HouseholdTask> builder)
    {
        builder.ToTable("household_tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Category).HasMaxLength(40).IsRequired().HasDefaultValue("General");

        // Recurrence as a readable string so DB rows are self-documenting.
        builder.Property(t => t.Recurrence).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Weekly bitmask persisted as smallint (fits all 7 flags + combinations).
        builder.Property(t => t.WeeklyDays).HasConversion<short?>();

        builder.Property(t => t.MonthlyDay);
        builder.Property(t => t.TimeOfDay);
        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.DueDate);
        builder.Property(t => t.IsArchived).HasDefaultValue(false);
        builder.Property(t => t.IsFloating).HasDefaultValue(false);

        // Points default 5 mirrors the new-task UX default and seeds existing
        // rows on the migration (HasDefaultValue is honoured by EF when adding
        // a non-nullable column to a populated table).
        builder.Property(t => t.Points).HasDefaultValue(5).IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(200);
        builder.Property(t => t.UpdatedBy).HasMaxLength(200);

        // FK to Family — restrict so a family with live tasks cannot be deleted.
        builder.HasOne(t => t.Family)
            .WithMany()
            .HasForeignKey(t => t.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to creator — restrict so deleting a member with authored tasks fails loud.
        builder.HasOne(t => t.CreatedByMember)
            .WithMany()
            .HasForeignKey(t => t.CreatedByMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to responsible member — set null on delete so removing a member doesn't
        // wipe their tasks; the task simply becomes "open" again.
        builder.HasOne(t => t.ResponsibleMember)
            .WithMany()
            .HasForeignKey(t => t.ResponsibleMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        // M:N with FamilyMember for related members (the "about whom" of the task,
        // not its executor — that's ResponsibleMemberId above). The join table is
        // explicit so schema diffs stay readable.
        builder.HasMany(t => t.RelatedMembers)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "household_task_related_members",
                right => right.HasOne<FamilyMember>()
                    .WithMany()
                    .HasForeignKey("family_member_id")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<HouseholdTask>()
                    .WithMany()
                    .HasForeignKey("task_id")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("household_task_related_members");
                    join.HasKey("task_id", "family_member_id");
                });

        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => new { t.FamilyId, t.IsArchived });
        builder.HasIndex(t => t.ResponsibleMemberId);
    }
}
