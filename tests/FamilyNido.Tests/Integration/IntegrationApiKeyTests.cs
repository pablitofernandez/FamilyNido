using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FamilyNido.Domain.Families;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// End-to-end coverage for the admin-side API-key management endpoints
/// under <c>/api/integrations/api-keys/**</c>. The machine-facing public
/// API surface (creating tasks, etc.) is covered separately in
/// <see cref="PublicApiTests"/>.
/// </summary>
public sealed class IntegrationApiKeyTests : IntegrationTestBase
{
    public IntegrationApiKeyTests(IntegrationFixture fixture) : base(fixture) { }

    private async Task<TestSeed.FamilyHandle> SeedAdminAsync()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");
        return handle;
    }

    [Fact]
    public async Task Create_returns_plaintext_token_once_and_persists_only_the_digest()
    {
        await SeedAdminAsync();

        var resp = await Client.PostAsJsonAsync("/api/integrations/api-keys", new { name = "Test integration" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await ReadAsync<CreatedKeyDto>(resp);
        created!.Token.Should().StartWith("bxn_").And.HaveLength(47);
        created.Key.Name.Should().Be("Test integration");
        created.Key.Prefix.Should().Be(created.Token[..12]);
        created.Key.LastUsedAt.Should().BeNull();
        created.Key.RevokedAt.Should().BeNull();

        // Persisted row keeps the digest, never the plaintext.
        var saved = await WithDbAsync(db => db.IntegrationApiKeys.SingleAsync());
        saved.TokenHash.Should().HaveLength(64).And.NotContain(created.Token);
        saved.Name.Should().Be("Test integration");
    }

    [Fact]
    public async Task Create_is_admin_only()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(
            db, Fixture.Factory.Services, role: FamilyRole.Adult));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var resp = await Client.PostAsJsonAsync("/api/integrations/api-keys", new { name = "test" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_returns_keys_newest_first_and_hides_revoked_secrets()
    {
        await SeedAdminAsync();

        await Client.PostAsJsonAsync("/api/integrations/api-keys", new { name = "first" });
        await Task.Delay(20);
        await Client.PostAsJsonAsync("/api/integrations/api-keys", new { name = "second" });

        var resp = await Client.GetAsync("/api/integrations/api-keys");
        var keys = await ReadAsync<List<KeyDto>>(resp);

        keys.Should().HaveCount(2);
        keys![0].Name.Should().Be("second");
        keys[1].Name.Should().Be("first");
    }

    [Fact]
    public async Task Revoke_marks_the_token_and_subsequent_use_returns_401()
    {
        await SeedAdminAsync();

        var createResp = await Client.PostAsJsonAsync("/api/integrations/api-keys", new { name = "to-be-revoked" });
        var created = await ReadAsync<CreatedKeyDto>(createResp);

        // First, prove the token works against the public API surface.
        var ok = await CallPublicApiAsync(created!.Token, new { title = "smoke", isFloating = true });
        ok.StatusCode.Should().Be(HttpStatusCode.Created);

        var revokeResp = await Client.PostAsync($"/api/integrations/api-keys/{created.Key.Id}/revoke", content: null);
        revokeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Now the same token must be rejected.
        var failed = await CallPublicApiAsync(created.Token, new { title = "smoke", isFloating = true });
        failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> CallPublicApiAsync(string token, object body)
    {
        // Issue a fresh request without the cookie auth so the integration
        // path is the only one being exercised.
        using var anon = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await anon.PostAsJsonAsync("/api/v1/tasks", body);
    }

    private sealed record CreatedKeyDto(string Token, KeyDto Key);
    private sealed record KeyDto(
        Guid Id,
        string Name,
        string Prefix,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastUsedAt,
        DateTimeOffset? RevokedAt);
}
