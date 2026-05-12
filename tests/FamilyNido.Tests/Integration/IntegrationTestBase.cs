using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FamilyNido.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Common base for integration tests. Resets the DB before each test, exposes
/// helpers for getting an <see cref="ApplicationDbContext"/> and an HTTP client,
/// and is opted into <see cref="IntegrationCollection"/> automatically so
/// concrete tests only need to inherit.
/// </summary>
[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    /// <summary>Shared fixture — Postgres + WebApplicationFactory.</summary>
    protected IntegrationFixture Fixture { get; }

    /// <summary>Test-scoped HTTP client. Cookies persist within the same test.</summary>
    protected HttpClient Client { get; private set; } = null!;

    /// <summary>Constructor — xUnit injects the fixture via the collection.</summary>
    protected IntegrationTestBase(IntegrationFixture fixture)
    {
        Fixture = fixture;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await Fixture.ResetAsync();
        Client = Fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            // Cookies are essential — local-login signs the session into one.
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Run the supplied delegate inside a fresh DI scope with an
    /// <see cref="ApplicationDbContext"/> available — used by tests to seed
    /// data or assert persisted state.
    /// </summary>
    protected async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    /// <summary>Void overload of <see cref="WithDbAsync{T}"/> for seeding helpers.</summary>
    protected async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(db);
    }

    /// <summary>
    /// JSON options that mirror the API: enums serialised as strings (the
    /// API registers <see cref="JsonStringEnumConverter"/> globally on
    /// <c>HttpJsonOptions</c>). Tests use this when reading bodies.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Read the response as <typeparamref name="T"/> using the API's JSON conventions.</summary>
    protected static Task<T?> ReadAsync<T>(HttpResponseMessage response)
        => response.Content.ReadFromJsonAsync<T>(JsonOptions);
}
