using System.Net.Http.Json;
using FamilyNido.Domain.HouseholdTasks;
using FluentAssertions;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for the family scoreboard slices: leaderboard aggregation and
/// per-member totals (this week / this month / all time). Seeds tasks +
/// completions directly through EF so we don't need to drive the UI flow
/// for every fixture variation.
/// </summary>
public sealed class ScoresTests : IntegrationTestBase
{
    public ScoresTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Leaderboard_sums_points_per_member_for_the_range()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var alice = await WithDbAsync(db => TestSeed.SeedMemberAsync(db, handle.Family.Id, "Alice"));

        // Dan: 5 points (one task), Alice: 12 points (two tasks).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await WithDbAsync(async db =>
        {
            var t1 = new HouseholdTask
            {
                FamilyId = handle.Family.Id,
                Title = "Tarea 5",
                StartDate = today,
                DueDate = today,
                CreatedByMemberId = handle.Member.Id,
                Points = 5,
            };
            var t2 = new HouseholdTask
            {
                FamilyId = handle.Family.Id,
                Title = "Tarea 7",
                StartDate = today,
                DueDate = today,
                CreatedByMemberId = handle.Member.Id,
                Points = 7,
            };
            var t3 = new HouseholdTask
            {
                FamilyId = handle.Family.Id,
                Title = "Tarea 5b",
                StartDate = today,
                DueDate = today,
                CreatedByMemberId = handle.Member.Id,
                Points = 5,
            };
            db.HouseholdTasks.AddRange(t1, t2, t3);
            await db.SaveChangesAsync();

            db.TaskCompletions.AddRange(
                new TaskCompletion
                {
                    TaskId = t1.Id, OccurrenceDate = today,
                    CompletedByMemberId = handle.Member.Id,
                    CompletedAt = DateTimeOffset.UtcNow,
                },
                new TaskCompletion
                {
                    TaskId = t2.Id, OccurrenceDate = today,
                    CompletedByMemberId = alice.Id,
                    CompletedAt = DateTimeOffset.UtcNow,
                },
                new TaskCompletion
                {
                    TaskId = t3.Id, OccurrenceDate = today,
                    CompletedByMemberId = alice.Id,
                    CompletedAt = DateTimeOffset.UtcNow,
                });
            await db.SaveChangesAsync();
        });

        var resp = await Client.GetAsync($"/api/scores/leaderboard?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        var board = await ReadAsync<LeaderboardDto>(resp);

        board!.Entries.Should().HaveCount(2);
        board.Entries[0].MemberId.Should().Be(alice.Id);
        board.Entries[0].Points.Should().Be(12);
        board.Entries[0].CompletionCount.Should().Be(2);
        board.Entries[1].MemberId.Should().Be(handle.Member.Id);
        board.Entries[1].Points.Should().Be(5);
    }

    [Fact]
    public async Task Member_score_returns_zeros_when_no_completions()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var resp = await Client.GetAsync($"/api/scores/members/{handle.Member.Id}");
        var score = await ReadAsync<MemberScoreDto>(resp);

        score!.MemberId.Should().Be(handle.Member.Id);
        score.ThisWeek.Should().Be(0);
        score.ThisMonth.Should().Be(0);
        score.AllTime.Should().Be(0);
    }

    private sealed record LeaderboardDto(string From, string To, List<ScoreboardEntry> Entries);
    private sealed record ScoreboardEntry(Guid MemberId, int Points, int CompletionCount);
    private sealed record MemberScoreDto(Guid MemberId, int ThisWeek, int ThisMonth, int AllTime);
}
