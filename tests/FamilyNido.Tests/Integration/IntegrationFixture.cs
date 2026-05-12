using FamilyNido.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Single shared fixture for the whole integration-test suite. Owns:
/// <list type="bullet">
/// <item>One Postgres container (TestContainers) — schema migrated once at start.</item>
/// <item>One <see cref="WebApplicationFactory{TEntryPoint}"/> bound to that container.</item>
/// <item>A <see cref="ResetAsync"/> primitive that truncates every public table
///   between tests to keep them independent.</item>
/// </list>
/// xUnit will instantiate this fixture once per test collection (see
/// <see cref="IntegrationCollection"/>) and dispose it at the end of the run.
/// </summary>
public sealed class IntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("FamilyNido")
        .WithUsername("FamilyNido")
        .WithPassword("FamilyNido")
        .Build();

    /// <summary>WebApplicationFactory bound to the Postgres container.</summary>
    public FamilyNidoApiFactory Factory { get; private set; } = null!;

    /// <summary>List of public tables to truncate, captured once after migrations.</summary>
    private string[] _tables = [];

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new FamilyNidoApiFactory(_postgres.GetConnectionString());

        // Triggering CreateClient() bootstraps the host, which also runs
        // migrations because Family.AutoMigrate=true in appsettings.Testing.json.
        using var bootstrap = Factory.CreateClient();

        // Capture the live table list (schema is now stable). Used by ResetAsync.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT tablename FROM pg_tables
                WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                """;
            var rows = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(reader.GetString(0));
            }
            _tables = [.. rows];
        }
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Wipe every public table between tests so they observe a known state.
    /// Cheaper than recreating the schema and works fine for our scale.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_tables.Length == 0) return;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        var quoted = string.Join(", ", _tables.Select(t => $"\"{t}\""));
        cmd.CommandText = $"TRUNCATE TABLE {quoted} RESTART IDENTITY CASCADE";
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>Marker collection so every integration test class shares the fixture above.</summary>
[CollectionDefinition(IntegrationCollection.Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
    /// <summary>Collection name referenced by tests via <c>[Collection(...)]</c>.</summary>
    public const string Name = "FamilyNido integration";
}

/// <summary>
/// Test-time WebApplicationFactory: rewires the Postgres connection string at
/// the configuration layer (rather than re-registering the DbContext) so the
/// real <see cref="DependencyInjection.AddFamilyNidoPersistence"/> wiring stays
/// in effect — including migrations + naming conventions.
/// </summary>
public sealed class FamilyNidoApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);

        // Fail fast on missing test config files: the test project copies
        // appsettings.Testing.json next to the assembly via the csproj.
        builder.ConfigureAppConfiguration((context, cfg) =>
        {
            var dir = AppContext.BaseDirectory;
            cfg.AddJsonFile(Path.Combine(dir, "appsettings.Testing.json"), optional: false);
        });

        // The production setup wires OIDC as the default challenge scheme so
        // the OnRedirectToLogin event on Cookie is the one that returns 401
        // for /api paths. Under test we have no real OIDC provider — keeping
        // OIDC as the challenge scheme means every anonymous /api hit tries
        // to fetch a bogus metadata document and crashes with 500. Switching
        // the default challenge to Cookie in the test environment is enough
        // to get clean 401s; the local-login slice signs into the cookie
        // scheme directly so it is not affected.
        builder.ConfigureTestServices(services =>
        {
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });
        });
    }
}
