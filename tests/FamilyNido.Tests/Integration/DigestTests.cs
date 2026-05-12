using System.Net.Http.Json;
using FamilyNido.Domain.HouseholdTasks;
using FluentAssertions;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for the manual digest preview endpoint
/// (<c>POST /api/notifications/digest/me</c>) — focused on the rule that
/// completed-today tasks must NOT appear in the email, while pending-today
/// tasks must.
/// </summary>
public sealed class DigestTests : IntegrationTestBase
{
    public DigestTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Digest_excludes_tasks_that_are_already_completed_for_today()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await WithDbAsync(async db =>
        {
            var task = new HouseholdTask
            {
                FamilyId = handle.Family.Id,
                Title = "Recoger el lavavajillas",
                Category = "Hogar",
                Recurrence = RecurrenceMode.None,
                StartDate = today,
                DueDate = today,
                IsFloating = false,
                Points = 3,
                CreatedByMemberId = handle.Member.Id,
            };
            db.HouseholdTasks.Add(task);
            await db.SaveChangesAsync();

            db.TaskCompletions.Add(new TaskCompletion
            {
                TaskId = task.Id,
                CompletedByMemberId = handle.Member.Id,
                OccurrenceDate = today,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        // The only thing seeded for today is a task that is already done.
        // The digest must therefore have nothing to report.
        var resp = await Client.PostAsync("/api/notifications/digest/me", content: null);
        resp.IsSuccessStatusCode.Should().BeTrue();

        var body = await ReadAsync<DigestResponse>(resp);
        body!.IsEmpty.Should().BeTrue("a task already completed today should not appear in the digest");
    }

    [Fact]
    public async Task Digest_includes_tasks_that_are_pending_for_today()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await WithDbAsync(async db =>
        {
            db.HouseholdTasks.Add(new HouseholdTask
            {
                FamilyId = handle.Family.Id,
                Title = "Recoger la lavadora",
                Category = "Hogar",
                Recurrence = RecurrenceMode.None,
                StartDate = today,
                DueDate = today,
                IsFloating = false,
                Points = 3,
                CreatedByMemberId = handle.Member.Id,
            });
            await db.SaveChangesAsync();
        });

        var resp = await Client.PostAsync("/api/notifications/digest/me", content: null);
        resp.IsSuccessStatusCode.Should().BeTrue();

        var body = await ReadAsync<DigestResponse>(resp);
        body!.IsEmpty.Should().BeFalse("a pending task for today should be reported");
    }

    private sealed record DigestResponse(string Email, bool IsEmpty);
}
