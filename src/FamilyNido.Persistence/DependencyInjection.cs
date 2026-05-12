using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyNido.Persistence;

/// <summary>
/// Composition root for the persistence layer. Registers EF Core against
/// PostgreSQL with snake_case naming conventions.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="ApplicationDbContext"/> against the "Postgres"
    /// connection string plus the default <see cref="TimeProvider"/> used for
    /// audit timestamps. Callers are responsible for registering a matching
    /// <see cref="ICurrentActorProvider"/>; if none is registered, audit
    /// columns default to <c>"system"</c>.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="configuration">Application configuration; reads connection string "Postgres".</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFamilyNidoPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Missing connection string 'Postgres'. Set ConnectionStrings__Postgres in configuration.");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
            options.UseSnakeCaseNamingConvention();
        });

        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }

        return services;
    }
}
