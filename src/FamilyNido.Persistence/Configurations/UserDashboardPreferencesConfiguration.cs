using FamilyNido.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="UserDashboardPreferences"/>.</summary>
public sealed class UserDashboardPreferencesConfiguration : IEntityTypeConfiguration<UserDashboardPreferences>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserDashboardPreferences> builder)
    {
        builder.ToTable("user_dashboard_preferences");

        builder.HasKey(p => p.UserId);

        builder.Property(p => p.WidgetsJson).HasColumnType("text").IsRequired();

        builder.HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserDashboardPreferences>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
