using FamilyNido.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyNido.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="UserCredential"/>.</summary>
public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials", t =>
        {
            // Coherence: an Oidc credential carries a provider key (sub) and no
            // password; a Local credential carries a password hash and no key.
            t.HasCheckConstraint(
                "ck_user_credentials_shape",
                "(provider = 0 AND provider_key IS NOT NULL AND password_hash IS NULL) "
              + "OR (provider = 1 AND provider_key IS NULL AND password_hash IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider).HasConversion<int>().IsRequired();
        builder.Property(c => c.ProviderKey).HasMaxLength(255);
        builder.Property(c => c.PasswordHash).HasMaxLength(512);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(200);
        builder.Property(c => c.UpdatedBy).HasMaxLength(200);

        // OIDC sub is globally unique across providers — we only ever support one
        // OIDC issuer per instance, so a partial unique index on (provider, key)
        // is enough to protect against duplicate accounts.
        builder.HasIndex(c => new { c.Provider, c.ProviderKey })
            .IsUnique()
            .HasFilter("provider_key IS NOT NULL");

        builder.HasIndex(c => c.UserId);
    }
}
