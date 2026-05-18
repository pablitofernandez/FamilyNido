using System.Net;
using System.Net.Http.Json;
using FamilyNido.Domain.Families;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for /api/wall: create, update, delete, comment, reaction toggle,
/// pin/unpin, markdown preview. Permission rules around author-or-admin are
/// also exercised.
/// </summary>
public sealed class WallTests : IntegrationTestBase
{
    public WallTests(IntegrationFixture fixture) : base(fixture) { }

    private async Task<TestSeed.FamilyHandle> SeedAdminAsync()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");
        return handle;
    }

    [Fact]
    public async Task Create_persists_a_message_and_renders_html()
    {
        await SeedAdminAsync();

        var resp = await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Hola **mundo**" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await ReadAsync<MessageRow>(resp);
        dto!.Text.Should().Contain("Hola");
        dto.TextHtml.Should().Contain("<strong>");
    }

    [Fact]
    public async Task Update_changes_text_when_caller_is_author()
    {
        await SeedAdminAsync();
        var created = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "v1" }));

        var resp = await Client.PatchAsJsonAsync(
            $"/api/wall/messages/{created!.Id}",
            new { text = "v2 editado" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await WithDbAsync(db =>
            db.WallMessages.Where(m => m.Id == created.Id).Select(m => m.Text).FirstAsync());
        saved.Should().Contain("v2 editado");
    }

    [Fact]
    public async Task Update_rejects_non_author_non_admin()
    {
        // Dan (admin) creates the message.
        var dan = await SeedAdminAsync();
        var created = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Original de Dan" }));

        // Create a second user (Adult) who is not the author and not admin.
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(
            db, Fixture.Factory.Services,
            email: "alice@example.com",
            displayName: "Alice",
            role: FamilyRole.Adult));
        // The second seed created a *new* family — back the test out and put
        // Alice in Dan's family instead so the message is visible.
        await WithDbAsync(async db =>
        {
            var alice = await db.Users.SingleAsync(u => u.Email == "alice@example.com");
            var aliceMember = await db.FamilyMembers.SingleAsync(m => m.UserId == alice.Id);
            aliceMember.FamilyId = dan.Family.Id;
            // Drop the second family so it doesn't pollute future queries.
            var spareFamily = await db.Families.SingleAsync(f => f.Id != dan.Family.Id);
            db.Families.Remove(spareFamily);
            await db.SaveChangesAsync();
        });

        // Re-login as Alice.
        var aliceClient = Fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        await TestSeed.LoginAsync(aliceClient, "alice@example.com");

        var resp = await aliceClient.PatchAsJsonAsync(
            $"/api/wall/messages/{created!.Id}",
            new { text = "Hijack" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_removes_the_message()
    {
        await SeedAdminAsync();
        var created = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Para borrar" }));

        var resp = await Client.DeleteAsync($"/api/wall/messages/{created!.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stillThere = await WithDbAsync(db =>
            db.WallMessages.AnyAsync(m => m.Id == created.Id));
        stillThere.Should().BeFalse();
    }

    [Fact]
    public async Task Pin_then_unpin_round_trips()
    {
        await SeedAdminAsync();
        var created = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Para fijar" }));

        var pin = await Client.PostAsync($"/api/wall/messages/{created!.Id}/pin", content: null);
        pin.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db =>
            db.WallMessages.Where(m => m.Id == created.Id).Select(m => m.IsPinned).FirstAsync()))
            .Should().BeTrue();

        var unpin = await Client.PostAsync($"/api/wall/messages/{created.Id}/unpin", content: null);
        unpin.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db =>
            db.WallMessages.Where(m => m.Id == created.Id).Select(m => m.IsPinned).FirstAsync()))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Comment_persists_and_delete_removes_it()
    {
        await SeedAdminAsync();
        var msg = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Padre" }));

        var addResp = await Client.PostAsJsonAsync(
            $"/api/wall/messages/{msg!.Id}/comments",
            new { text = "Comentario" });
        addResp.IsSuccessStatusCode.Should().BeTrue();

        var comment = await ReadAsync<CommentRow>(addResp);
        comment!.Text.Should().Contain("Comentario");

        var del = await Client.DeleteAsync($"/api/wall/comments/{comment.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stillThere = await WithDbAsync(db =>
            db.WallComments.AnyAsync(c => c.Id == comment.Id));
        stillThere.Should().BeFalse();
    }

    [Fact]
    public async Task Toggle_reaction_adds_then_removes()
    {
        await SeedAdminAsync();
        var msg = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Reactionable" }));

        var first = await Client.PostAsJsonAsync(
            $"/api/wall/messages/{msg!.Id}/reactions",
            new { emoji = "❤️" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db =>
            db.WallReactions.CountAsync(r => r.MessageId == msg.Id))).Should().Be(1);

        var second = await Client.PostAsJsonAsync(
            $"/api/wall/messages/{msg.Id}/reactions",
            new { emoji = "❤️" });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db =>
            db.WallReactions.CountAsync(r => r.MessageId == msg.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Preview_renders_markdown_with_strong()
    {
        await SeedAdminAsync();

        var resp = await Client.PostAsJsonAsync("/api/wall/preview", new { text = "Hola **mundo**" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await ReadAsync<PreviewDto>(resp);
        dto!.Html.Should().Contain("<strong>mundo</strong>");
    }

    [Fact]
    public async Task GetWallMessage_returns_200_for_own_family()
    {
        await SeedAdminAsync();
        var created = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Mensaje propio" }));

        var resp = await Client.GetAsync($"/api/wall/messages/{created!.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadAsync<MessageRow>(resp);
        dto!.Id.Should().Be(created.Id);
        dto.Text.Should().Contain("Mensaje propio");
    }

    [Fact]
    public async Task GetWallMessage_returns_404_for_other_family()
    {
        // Family A — Dan creates a message that should be invisible to family B.
        await SeedAdminAsync();
        var created = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Privado de la familia A" }));

        // Family B — a fresh family with its own admin (Bob). SeedFamilyAsync
        // always creates a new family per call, so this is genuinely independent.
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(
            db, Fixture.Factory.Services,
            email: "bob@example.com",
            displayName: "Bob",
            role: FamilyRole.Admin));

        var bobClient = Fixture.Factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
        await TestSeed.LoginAsync(bobClient, "bob@example.com");

        var resp = await bobClient.GetAsync($"/api/wall/messages/{created!.Id}");

        // Handler returns NotFound (not Forbidden) on purpose to avoid leaking
        // whether the id exists outside the caller's family.
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWallMessage_returns_401_without_cookie()
    {
        await SeedAdminAsync();
        var created = await ReadAsync<MessageRow>(
            await Client.PostAsJsonAsync("/api/wall/messages", new { text = "Cualquier mensaje" }));

        // A second client without login → cookie scheme should challenge with 401.
        var anonymous = Fixture.Factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

        var resp = await anonymous.GetAsync($"/api/wall/messages/{created!.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record MessageRow(Guid Id, string Text, string TextHtml, bool IsPinned);
    private sealed record CommentRow(Guid Id, Guid MessageId, string Text);
    private sealed record PreviewDto(string Html);
}
