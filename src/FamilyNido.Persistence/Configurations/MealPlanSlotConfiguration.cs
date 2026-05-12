using FamilyNido.Domain.Meals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="MealPlanSlot"/>.</summary>
public sealed class MealPlanSlotConfiguration : IEntityTypeConfiguration<MealPlanSlot>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MealPlanSlot> builder)
    {
        builder.ToTable("meal_plan_slots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Date).IsRequired();
        // Slot stored as text ("Lunch"/"Dinner") so a quick `psql` peek is
        // self-documenting; trivially extends to Breakfast/Snack later.
        builder.Property(s => s.Slot).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.FirstCourse).HasMaxLength(120);
        builder.Property(s => s.SecondCourse).HasMaxLength(120);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.CreatedBy).HasMaxLength(200);
        builder.Property(s => s.UpdatedBy).HasMaxLength(200);

        builder.HasOne(s => s.Family)
            .WithMany()
            .HasForeignKey(s => s.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per (family, date, slot): upsert key.
        builder.HasIndex(s => new { s.FamilyId, s.Date, s.Slot }).IsUnique();
        // Powers the autocomplete query (prefix search across both courses).
        builder.HasIndex(s => new { s.FamilyId, s.FirstCourse });
        builder.HasIndex(s => new { s.FamilyId, s.SecondCourse });
    }
}
