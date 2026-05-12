using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FamilyNido.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling to build a context without
/// going through the ASP.NET host. Points at a placeholder local database; the
/// migrations generated here are provider-specific (PostgreSQL) but do not execute
/// against any real server.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <inheritdoc />
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=FamilyNido;Username=FamilyNido;Password=FamilyNido")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }
}
