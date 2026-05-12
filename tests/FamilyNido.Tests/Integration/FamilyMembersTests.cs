using System.Net;
using System.Net.Http.Json;
using FamilyNido.Domain.Families;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// CRUD + permission coverage for /api/family-members. Exercises every
/// endpoint mapped in <c>FamilyMemberEndpoints</c> plus the admin gate.
/// </summary>
public sealed class FamilyMembersTests : IntegrationTestBase
{
    public FamilyMembersTests(IntegrationFixture fixture) : base(fixture) { }

    private async Task<TestSeed.FamilyHandle> SeedAdminAsync()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");
        return handle;
    }

    [Fact]
    public async Task List_returns_admin_member_after_seed()
    {
        await SeedAdminAsync();

        var response = await Client.GetAsync("/api/family-members");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await ReadAsync<List<MemberRow>>(response);
        members.Should().NotBeNull();
        members.Should().HaveCount(1);
        members![0].DisplayName.Should().Be("Dan");
        members[0].HasAccount.Should().BeTrue();
        members[0].Role.Should().Be(FamilyRole.Admin);
    }

    [Fact]
    public async Task Create_persists_new_member()
    {
        await SeedAdminAsync();

        var body = new
        {
            displayName = "Bob",
            memberType = "Child",
            colorHex = "#7BA4A8",
            birthDate = "2018-05-15",
            contactEmail = (string?)null,
        };
        var response = await Client.PostAsJsonAsync("/api/family-members", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Persisted row matches the input.
        var saved = await WithDbAsync(db => db.FamilyMembers
            .Where(m => m.DisplayName == "Bob")
            .FirstAsync());
        saved.MemberType.Should().Be(MemberType.Child);
        saved.ColorHex.Should().Be("#7BA4A8");
        saved.BirthDate.Should().Be(new DateOnly(2018, 5, 15));
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_rejects_invalid_color()
    {
        await SeedAdminAsync();

        var body = new
        {
            displayName = "Bob",
            memberType = "Child",
            colorHex = "not-a-color",
        };
        var response = await Client.PostAsJsonAsync("/api/family-members", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_changes_display_name_and_color()
    {
        var handle = await SeedAdminAsync();
        var added = await WithDbAsync(db =>
            TestSeed.SeedMemberAsync(db, handle.Family.Id, "Bob"));

        var body = new
        {
            displayName = "Telmito",
            colorHex = "#FF0000",
            birthDate = (string?)null,
            contactEmail = (string?)null,
        };
        var response = await Client.PutAsJsonAsync($"/api/family-members/{added.Id}", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reloaded = await WithDbAsync(db =>
            db.FamilyMembers.Where(m => m.Id == added.Id).FirstAsync());
        reloaded.DisplayName.Should().Be("Telmito");
        reloaded.ColorHex.Should().Be("#FF0000");
    }

    [Fact]
    public async Task Deactivate_then_activate_round_trips()
    {
        var handle = await SeedAdminAsync();
        var added = await WithDbAsync(db =>
            TestSeed.SeedMemberAsync(db, handle.Family.Id, "Charlie"));

        var off = await Client.PatchAsync($"/api/family-members/{added.Id}/deactivate", content: null);
        off.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterOff = await WithDbAsync(db =>
            db.FamilyMembers.Where(m => m.Id == added.Id).FirstAsync());
        afterOff.IsActive.Should().BeFalse();

        var on = await Client.PatchAsync($"/api/family-members/{added.Id}/activate", content: null);
        on.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterOn = await WithDbAsync(db =>
            db.FamilyMembers.Where(m => m.Id == added.Id).FirstAsync());
        afterOn.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_removes_the_row()
    {
        var handle = await SeedAdminAsync();
        var added = await WithDbAsync(db =>
            TestSeed.SeedMemberAsync(db, handle.Family.Id, "Aitite"));

        var response = await Client.DeleteAsync($"/api/family-members/{added.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stillThere = await WithDbAsync(db =>
            db.FamilyMembers.AnyAsync(m => m.Id == added.Id));
        stillThere.Should().BeFalse();
    }

    [Fact]
    public async Task Non_admin_cannot_create_member()
    {
        // Seed an Adult (not Admin).
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(
            db, Fixture.Factory.Services,
            email: "alice@example.com",
            displayName: "Alice",
            role: FamilyRole.Adult));
        await TestSeed.LoginAsync(Client, "alice@example.com");

        var body = new
        {
            displayName = "Bob",
            memberType = "Child",
            colorHex = "#7BA4A8",
        };
        var response = await Client.PostAsJsonAsync("/api/family-members", body);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Compact projection used to deserialize the list response.</summary>
    private sealed record MemberRow(
        Guid Id,
        string DisplayName,
        MemberType MemberType,
        string ColorHex,
        DateOnly? BirthDate,
        string? ContactEmail,
        string? PhotoPath,
        bool IsActive,
        bool HasAccount,
        FamilyRole? Role);
}
