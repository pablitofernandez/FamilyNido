using FamilyNido.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="UserNotificationPreferences"/>.</summary>
public sealed class UserNotificationPreferencesConfiguration : IEntityTypeConfiguration<UserNotificationPreferences>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserNotificationPreferences> builder)
    {
        builder.ToTable("user_notification_preferences");

        builder.HasKey(p => p.UserId);

        builder.Property(p => p.EmailEnabled).HasDefaultValue(true);
        builder.Property(p => p.DigestEnabled).HasDefaultValue(true);
        builder.Property(p => p.TaskAssignedEnabled).HasDefaultValue(true);
        builder.Property(p => p.WallMentionEnabled).HasDefaultValue(true);

        // Cascade so removing a user wipes their preferences automatically.
        builder.HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserNotificationPreferences>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
