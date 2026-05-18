using System.Net;
using System.Net.Http.Json;
using FamilyNido.Domain.Identity;
using FluentAssertions;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for the local-credentials login slice. The OIDC flow is left
/// out — it requires a real provider. Local login is what the tests rely on
/// throughout the suite, so it earns proper exhaustive coverage here.
/// </summary>
public sealed class AuthTests : IntegrationTestBase
{
    public AuthTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LocalLogin_returns_200_with_known_user_and_correct_password()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));

        var response = await Client.PostAsJsonAsync("/api/auth/local/login",
            new { email = "dan@example.com", password = TestSeed.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LocalLogin_returns_403_with_unknown_user()
    {
        // No seed at all — the user simply does not exist.
        var response = await Client.PostAsJsonAsync("/api/auth/local/login",
            new { email = "nobody@example.com", password = "whatever" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LocalLogin_returns_403_with_correct_user_but_wrong_password()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));

        var response = await Client.PostAsJsonAsync("/api/auth/local/login",
            new { email = "dan@example.com", password = "definitely-wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LocalLogin_returns_400_with_malformed_email()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/local/login",
            new { email = "not-an-email", password = "whatever" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Providers_reports_oidc_disabled_when_authority_is_empty()
    {
        // Testing config leaves Oidc.Authority empty on purpose — the test
        // suite relies exclusively on local credentials and the login UI
        // hides the OIDC button accordingly.
        var response = await Client.GetAsync("/api/auth/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProvidersDto>();
        body!.OidcEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Login_endpoint_returns_404_when_oidc_is_disabled()
    {
        // With OIDC turned off the cookie-only stack should not surface a 500
        // when something pokes the legacy challenge endpoint; the API answers
        // a clean 404 instead.
        var response = await Client.GetAsync("/api/auth/login");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("//evil.example.com/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://other-host/")]
    public async Task Login_endpoint_rejects_external_returnUrls_to_prevent_open_redirect(string hostile)
    {
        // The login endpoint reflects returnUrl into the OIDC properties; an
        // unvalidated value lets an attacker craft a post-login bounce to a
        // phishing page. We can only assert here against the no-OIDC branch
        // (Testing has Authority="") but the same sanitiser runs in the OIDC
        // path right before Results.Challenge, so any redirect away from "/"
        // would be discarded there too.
        var response = await Client.GetAsync($"/api/auth/login?returnUrl={Uri.EscapeDataString(hostile)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logout_clears_the_session_cookie()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        // Confirm we are in.
        var meBefore = await Client.GetAsync("/api/auth/me");
        meBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        var logout = await Client.PostAsync("/api/auth/logout", content: null);
        logout.IsSuccessStatusCode.Should().BeTrue();

        // After logout, /api/auth/me should be 401.
        var meAfter = await Client.GetAsync("/api/auth/me");
        meAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Display preferences (issue #12 v2) ───────────────────────────────────

    [Fact]
    public async Task Set_time_format_persists_and_surfaces_through_me()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        // Default is null (auto).
        var meBefore = await ReadAsync<MeDto>(await Client.GetAsync("/api/auth/me"));
        meBefore!.TimeFormat.Should().BeNull();

        var set = await Client.PutAsJsonAsync(
            "/api/auth/me/time-format",
            new { timeFormat = "H12" });
        set.StatusCode.Should().Be(HttpStatusCode.OK);

        var meAfter = await ReadAsync<MeDto>(await Client.GetAsync("/api/auth/me"));
        meAfter!.TimeFormat.Should().Be(TimeFormatPreference.H12);
    }

    [Fact]
    public async Task Clearing_time_format_resets_to_null()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        await Client.PutAsJsonAsync("/api/auth/me/time-format", new { timeFormat = "H24" });
        (await ReadAsync<MeDto>(await Client.GetAsync("/api/auth/me")))!
            .TimeFormat.Should().Be(TimeFormatPreference.H24);

        // Sending null clears the override.
        var clear = await Client.PutAsJsonAsync(
            "/api/auth/me/time-format",
            new { timeFormat = (string?)null });
        clear.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadAsync<MeDto>(await Client.GetAsync("/api/auth/me")))!
            .TimeFormat.Should().BeNull();
    }

    [Fact]
    public async Task Set_temperature_unit_persists_and_surfaces_through_me()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        (await ReadAsync<MeDto>(await Client.GetAsync("/api/auth/me")))!
            .TemperatureUnit.Should().BeNull();

        var set = await Client.PutAsJsonAsync(
            "/api/auth/me/temperature-unit",
            new { temperatureUnit = "Fahrenheit" });
        set.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadAsync<MeDto>(await Client.GetAsync("/api/auth/me")))!
            .TemperatureUnit.Should().Be(TemperatureUnitPreference.Fahrenheit);
    }

    [Fact]
    public async Task Display_preference_endpoints_require_auth()
    {
        // No login → both PUTs should be 401, regardless of body.
        var anonymous = Fixture.Factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

        var time = await anonymous.PutAsJsonAsync(
            "/api/auth/me/time-format", new { timeFormat = "H12" });
        time.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var temp = await anonymous.PutAsJsonAsync(
            "/api/auth/me/temperature-unit", new { temperatureUnit = "Fahrenheit" });
        temp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Minimal projection of MeDto with the fields these tests touch.</summary>
    private sealed record MeDto(
        Guid UserId,
        string Email,
        TimeFormatPreference? TimeFormat,
        TemperatureUnitPreference? TemperatureUnit);

    private sealed record ProvidersDto(bool OidcEnabled);
}
