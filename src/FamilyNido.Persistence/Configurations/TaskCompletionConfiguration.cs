using FamilyNido.Domain.HouseholdTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="TaskCompletion"/>.</summary>
public sealed class TaskCompletionConfiguration : IEntityTypeConfiguration<TaskCompletion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskCompletion> builder)
    {
        builder.ToTable("task_completions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.OccurrenceDate).IsRequired();
        builder.Property(c => c.CompletedAt).IsRequired();
        builder.Property(c => c.Note).HasMaxLength(500);

        builder.HasOne(c => c.Task)
            .WithMany(t => t.Completions)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // SET NULL preserves the completion row when the member is deleted.
        builder.HasOne(c => c.CompletedBy)
            .WithMany()
            .HasForeignKey(c => c.CompletedByMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => new { c.TaskId, c.OccurrenceDate }).IsUnique();
    }
}
