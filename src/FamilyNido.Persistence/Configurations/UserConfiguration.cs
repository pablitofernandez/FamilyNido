using FamilyNido.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="User"/>.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        // BCP-47 tag, e.g. "es-ES" or "en-US". Stored as a string so adding a
        // new locale never needs a schema change.
        builder.Property(u => u.PreferredLanguage).HasMaxLength(16).IsRequired().HasDefaultValue("es-ES");

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.CreatedBy).HasMaxLength(200);
        builder.Property(u => u.UpdatedBy).HasMaxLength(200);

        builder.HasIndex(u => u.Email).IsUnique();

        // Cascade delete a user → all its credentials. Not the other way around:
        // a user without credentials still exists (briefly) during credential
        // rotation, the API just won't authenticate them.
        builder.HasMany(u => u.Credentials)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
