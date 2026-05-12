using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FamilyNido.Domain.HouseholdTasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// End-to-end coverage for the public API surface under <c>/api/v1/**</c>:
/// generic task creation by integrations holding a valid API key.
/// </summary>
public sealed class PublicApiTests : IntegrationTestBase
{
    public PublicApiTests(IntegrationFixture fixture) : base(fixture) { }

    private async Task<(TestSeed.FamilyHandle handle, string token)> SeedAndIssueTokenAsync(
        string tokenName = "Test integration")
    {
        var handle = await WithDbAsync(db =>
            TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var resp = await Client.PostAsJsonAsync("/api/integrations/api-keys", new { name = tokenName });
        resp.IsSuccessStatusCode.Should().BeTrue();
        var created = await ReadAsync<CreatedKeyDto>(resp);
        return (handle, created!.Token);
    }

    /// <summary>Issue a request that exercises the integration API-key path only (no cookie).</summary>
    private async Task<HttpResponseMessage> PostTaskAsync(string token, object body)
    {
        using var anon = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await anon.PostAsJsonAsync("/api/v1/tasks", body);
    }

    [Fact]
    public async Task Post_without_auth_returns_401()
    {
        await SeedAndIssueTokenAsync();

        var resp = await Client.PostAsJsonAsync("/api/v1/tasks", new { title = "test" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_with_unknown_token_returns_401()
    {
        var resp = await PostTaskAsync("bxn_thisisnotreal", new { title = "test" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_minimal_body_creates_a_floating_chore_today()
    {
        var (handle, token) = await SeedAndIssueTokenAsync();

        var resp = await PostTaskAsync(token, new { title = "Comprar pan" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadAsync<TaskResponse>(resp);
        body!.Created.Should().BeTrue();
        body.Title.Should().Be("Comprar pan");

        var saved = await WithDbAsync(db => db.HouseholdTasks.SingleAsync());
        saved.FamilyId.Should().Be(handle.Family.Id);
        saved.CreatedByMemberId.Should().Be(handle.Member.Id);
        saved.Title.Should().Be("Comprar pan");
        saved.Category.Should().Be("General");
        saved.Points.Should().Be(5);
        saved.IsFloating.Should().BeFalse();
        // Single-shot non-floating task with no DueDate supplied → falls back to today.
        saved.DueDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
        saved.Recurrence.Should().Be(RecurrenceMode.None);
        saved.ResponsibleMemberId.Should().BeNull();
    }

    [Fact]
    public async Task Post_with_isFloating_true_creates_a_floating_task()
    {
        var (_, token) = await SeedAndIssueTokenAsync();

        var resp = await PostTaskAsync(token, new
        {
            title = "Vaciar el lavavajillas",
            category = "Hogar",
            points = 3,
            isFloating = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var saved = await WithDbAsync(db => db.HouseholdTasks.SingleAsync());
        saved.Category.Should().Be("Hogar");
        saved.Points.Should().Be(3);
        saved.IsFloating.Should().BeTrue();
        saved.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Post_with_explicit_dueDate_honours_it()
    {
        var (_, token) = await SeedAndIssueTokenAsync();
        var due = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

        var resp = await PostTaskAsync(token, new
        {
            title = "Renovar DNI",
            dueDate = due.ToString("yyyy-MM-dd"),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var saved = await WithDbAsync(db => db.HouseholdTasks.SingleAsync());
        saved.DueDate.Should().Be(due);
        saved.IsFloating.Should().BeFalse();
    }

    [Fact]
    public async Task Post_with_deduplicate_true_returns_200_on_second_call()
    {
        var (_, token) = await SeedAndIssueTokenAsync();

        var first = await PostTaskAsync(token, new
        {
            title = "Vaciar el lavavajillas",
            isFloating = true,
            deduplicate = true,
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await ReadAsync<TaskResponse>(first);

        var second = await PostTaskAsync(token, new
        {
            title = "Vaciar el lavavajillas",
            isFloating = true,
            deduplicate = true,
        });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await ReadAsync<TaskResponse>(second);
        secondBody!.Created.Should().BeFalse();
        secondBody.Reason.Should().Be("already-pending");
        secondBody.TaskId.Should().Be(firstBody!.TaskId);

        var count = await WithDbAsync(db => db.HouseholdTasks.CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Post_with_deduplicate_creates_new_task_after_previous_was_completed()
    {
        var (handle, token) = await SeedAndIssueTokenAsync();

        await PostTaskAsync(token, new
        {
            title = "Tender la lavadora",
            isFloating = true,
            deduplicate = true,
        });

        // Mark the floating task done so its "pending" status flips.
        await WithDbAsync(async db =>
        {
            var task = await db.HouseholdTasks.SingleAsync();
            db.TaskCompletions.Add(new TaskCompletion
            {
                TaskId = task.Id,
                CompletedByMemberId = handle.Member.Id,
                OccurrenceDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var second = await PostTaskAsync(token, new
        {
            title = "Tender la lavadora",
            isFloating = true,
            deduplicate = true,
        });
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var count = await WithDbAsync(db => db.HouseholdTasks.CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Post_with_deduplicate_false_always_creates_a_new_row()
    {
        var (_, token) = await SeedAndIssueTokenAsync();

        await PostTaskAsync(token, new { title = "Recoger paquete", isFloating = true });
        await PostTaskAsync(token, new { title = "Recoger paquete", isFloating = true });

        var count = await WithDbAsync(db => db.HouseholdTasks.CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Post_with_responsibleMemberId_outside_the_family_returns_400()
    {
        var (_, token) = await SeedAndIssueTokenAsync();
        var foreignMemberId = Guid.NewGuid();

        var resp = await PostTaskAsync(token, new
        {
            title = "Recoger paquete",
            isFloating = true,
            responsibleMemberId = foreignMemberId,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_with_invalid_points_returns_400()
    {
        var (_, token) = await SeedAndIssueTokenAsync();

        var resp = await PostTaskAsync(token, new { title = "x", points = -5 });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_with_empty_title_returns_400()
    {
        var (_, token) = await SeedAndIssueTokenAsync();

        var resp = await PostTaskAsync(token, new { title = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record CreatedKeyDto(string Token, KeyDto Key);
    private sealed record KeyDto(
        Guid Id,
        string Name,
        string Prefix,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastUsedAt,
        DateTimeOffset? RevokedAt);
    private sealed record TaskResponse(bool Created, string? Reason, Guid TaskId, string Title);
}
