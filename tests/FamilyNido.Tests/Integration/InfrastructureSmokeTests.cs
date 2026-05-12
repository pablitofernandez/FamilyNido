using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Sanity checks for the integration-test infrastructure itself:
/// the Postgres container is reachable, migrations applied, the
/// WebApplicationFactory bootstraps, and the local-login + cookie pipeline
/// works end-to-end. Every other integration spec assumes these things;
/// keeping a smoke here makes "the infra broke" obvious from the failure list.
/// </summary>
public sealed class InfrastructureSmokeTests : IntegrationTestBase
{
    public InfrastructureSmokeTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Database_is_migrated_and_empty()
    {
        var familyCount = await WithDbAsync(db => db.Families.CountAsync());
        familyCount.Should().Be(0);
    }

    [Fact]
    public async Task Anonymous_request_to_protected_endpoint_returns_401()
    {
        var response = await Client.GetAsync("/api/family-members");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Local_login_grants_an_authenticated_session_cookie()
    {
        // Seed a family + admin user with the default test password.
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));

        // Login via the same endpoint the front end uses.
        await TestSeed.LoginAsync(Client, "dan@example.com");

        // The cookie is now sticky on the client; protected endpoints succeed.
        var response = await Client.GetAsync("/api/family-members");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_between_tests_actually_truncates()
    {
        // This test relies on the previous test having seeded a family. By the
        // time we run, IntegrationTestBase.InitializeAsync has called ResetAsync,
        // so the table count must be back to zero.
        var count = await WithDbAsync(db => db.Users.CountAsync());
        count.Should().Be(0);
    }
}
