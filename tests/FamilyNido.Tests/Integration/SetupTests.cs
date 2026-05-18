using System.Net;
using System.Net.Http.Json;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for the first-run setup wizard (issue #20). Status probes are
/// anonymous; the bootstrap call is anonymous-but-one-shot and creates the
/// full Family + User + FamilyMember + UserCredential graph.
/// </summary>
public sealed class SetupTests : IntegrationTestBase
{
    public SetupTests(IntegrationFixture fixture) : base(fixture) { }

    private static object NewSetupBody(
        string familyName = "Familia Test",
        string timeZone = "Europe/Madrid",
        string email = "admin@example.com",
        string displayName = "Pablo",
        string password = "Sup3rSecret!")
        => new
        {
            family = new { name = familyName, timeZone },
            admin = new { email, displayName, password },
        };

    [Fact]
    public async Task Status_returns_false_on_an_empty_database()
    {
        var resp = await Client.GetAsync("/api/setup/status");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadAsync<StatusDto>(resp);
        dto!.Initialized.Should().BeFalse();
    }

    [Fact]
    public async Task Status_returns_true_after_initialization()
    {
        var setup = await Client.PostAsJsonAsync("/api/setup/initial-admin", NewSetupBody());
        setup.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await ReadAsync<StatusDto>(await Client.GetAsync("/api/setup/status"));
        dto!.Initialized.Should().BeTrue();
    }

    [Fact]
    public async Task Initial_admin_creates_the_full_graph_and_logs_in()
    {
        var setup = await Client.PostAsJsonAsync("/api/setup/initial-admin",
            NewSetupBody(
                familyName: "Familia García",
                timeZone: "America/New_York",
                email: "marshall@example.com",
                displayName: "Marshall"));
        setup.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The four rows that make a working instance: family + admin + linked
        // member + local credential. Verifying them together catches any
        // wiring regression in the handler's transaction.
        var snapshot = await WithDbAsync(async db => new
        {
            Family = await db.Families.FirstOrDefaultAsync(),
            User = await db.Users.FirstOrDefaultAsync(),
            Member = await db.FamilyMembers.FirstOrDefaultAsync(),
            Credential = await db.UserCredentials.FirstOrDefaultAsync(),
        });

        snapshot.Family!.Name.Should().Be("Familia García");
        snapshot.Family.TimeZone.Should().Be("America/New_York");
        snapshot.Family.Locale.Should().Be("en-US");

        snapshot.User!.Email.Should().Be("marshall@example.com");
        snapshot.User.DisplayName.Should().Be("Marshall");
        snapshot.User.Role.Should().Be(FamilyRole.Admin);
        snapshot.User.PreferredLanguage.Should().Be("en-US");

        snapshot.Member!.UserId.Should().Be(snapshot.User.Id);
        snapshot.Member.FamilyId.Should().Be(snapshot.Family.Id);
        snapshot.Member.MemberType.Should().Be(MemberType.Adult);

        snapshot.Credential!.Provider.Should().Be(IdentityProvider.Local);
        snapshot.Credential.PasswordHash.Should().NotBeNullOrEmpty();
        snapshot.Credential.PasswordHash.Should().NotContain("Sup3rSecret",
            "the credential must store a hash, not the plain-text password");

        // The freshly-minted credential should accept the same password via
        // the regular local-login endpoint — that's the round-trip the SPA
        // does right after the wizard finishes.
        var login = await Client.PostAsJsonAsync("/api/auth/local/login",
            new { email = "marshall@example.com", password = "Sup3rSecret!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Initial_admin_refuses_with_409_once_a_user_exists()
    {
        await Client.PostAsJsonAsync("/api/setup/initial-admin", NewSetupBody());

        var second = await Client.PostAsJsonAsync(
            "/api/setup/initial-admin",
            NewSetupBody(email: "second@example.com", displayName: "Intruso"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Initial_admin_rejects_unknown_timezone()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/setup/initial-admin",
            NewSetupBody(timeZone: "Mars/Olympus"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Initial_admin_rejects_weak_password()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/setup/initial-admin",
            NewSetupBody(password: "short"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Initial_admin_rejects_invalid_email()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/setup/initial-admin",
            NewSetupBody(email: "not-an-email"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record StatusDto(bool Initialized);
}
