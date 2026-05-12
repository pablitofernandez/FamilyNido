using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for /api/member-agenda: pattern CRUD, ad-hoc + override
/// exceptions, and the overview resolver that merges them. The resolver
/// has the most edge cases of any read-side slice in the app, so this
/// suite is intentionally exhaustive.
/// </summary>
public sealed class MemberAgendaTests : IntegrationTestBase
{
    public MemberAgendaTests(IntegrationFixture fixture) : base(fixture) { }

    private async Task<TestSeed.FamilyHandle> SeedAdminAsync()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");
        return handle;
    }

    [Fact]
    public async Task Create_pattern_persists_and_overview_returns_resolved_entries()
    {
        var handle = await SeedAdminAsync();

        var create = await Client.PostAsJsonAsync("/api/member-agenda/patterns", new
        {
            memberId = handle.Member.Id,
            dayOfWeek = "Tuesday",
            label = "Mondragón",
            location = "Mondragón",
            startTime = "08:30:00",
            endTime = "18:00:00",
            transportMode = "Car",
            isAway = true,
            notes = (string?)null,
            isActive = true,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        // Pick a Tuesday in range to assert resolution. Look at the next
        // 14 days for a Tuesday so the test isn't day-of-week dependent.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tuesday = today;
        for (var i = 0; i < 14; i++)
        {
            if (today.AddDays(i).DayOfWeek == DayOfWeek.Tuesday)
            {
                tuesday = today.AddDays(i);
                break;
            }
        }

        var resp = await Client.GetAsync(
            $"/api/member-agenda/overview?from={tuesday:yyyy-MM-dd}&to={tuesday:yyyy-MM-dd}");
        var overview = await ReadAsync<OverviewDto>(resp);

        overview!.Resolved.Should().Contain(r =>
            r.MemberId == handle.Member.Id &&
            r.Label == "Mondragón" &&
            r.IsAway);
    }

    [Fact]
    public async Task Override_exception_cancels_a_pattern_for_one_date()
    {
        var handle = await SeedAdminAsync();

        var patternResp = await Client.PostAsJsonAsync("/api/member-agenda/patterns", new
        {
            memberId = handle.Member.Id,
            dayOfWeek = "Tuesday",
            label = "Mondragón",
            transportMode = "Car",
            isAway = true,
            isActive = true,
            location = (string?)null,
            startTime = (string?)null,
            endTime = (string?)null,
            notes = (string?)null,
        });
        var pattern = await ReadAsync<PatternDto>(patternResp);

        // Find next Tuesday and cancel it via an override exception.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tuesday = today;
        for (var i = 0; i < 14; i++)
        {
            if (today.AddDays(i).DayOfWeek == DayOfWeek.Tuesday)
            {
                tuesday = today.AddDays(i);
                break;
            }
        }

        await Client.PostAsJsonAsync("/api/member-agenda/exceptions", new
        {
            memberId = handle.Member.Id,
            date = tuesday.ToString("yyyy-MM-dd"),
            patternId = pattern!.Id,
            isCancelled = true,
            label = (string?)null,
            location = (string?)null,
            startTime = (string?)null,
            endTime = (string?)null,
            transportMode = (string?)null,
            isAway = (bool?)null,
            notes = (string?)null,
        });

        var resp = await Client.GetAsync(
            $"/api/member-agenda/overview?from={tuesday:yyyy-MM-dd}&to={tuesday:yyyy-MM-dd}");
        var overview = await ReadAsync<OverviewDto>(resp);

        // Cancelled — must not appear in the resolved list.
        overview!.Resolved.Should().NotContain(r => r.PatternId == pattern.Id);
    }

    [Fact]
    public async Task Adhoc_exception_appears_in_overview_for_that_single_date()
    {
        var handle = await SeedAdminAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        await Client.PostAsJsonAsync("/api/member-agenda/exceptions", new
        {
            memberId = handle.Member.Id,
            date = date.ToString("yyyy-MM-dd"),
            patternId = (Guid?)null,
            isCancelled = false,
            label = "Día puntual",
            location = "Madrid",
            startTime = "10:00:00",
            endTime = (string?)null,
            transportMode = "Train",
            isAway = true,
            notes = (string?)null,
        });

        var resp = await Client.GetAsync(
            $"/api/member-agenda/overview?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");
        var overview = await ReadAsync<OverviewDto>(resp);

        overview!.Resolved.Should().Contain(r =>
            r.PatternId == null &&
            r.Label == "Día puntual" &&
            r.Location == "Madrid");
    }

    [Fact]
    public async Task Delete_pattern_cascades_to_its_overrides()
    {
        var handle = await SeedAdminAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var patternResp = await Client.PostAsJsonAsync("/api/member-agenda/patterns", new
        {
            memberId = handle.Member.Id,
            dayOfWeek = "Monday",
            label = "Gym",
            transportMode = "Walk",
            isAway = true,
            isActive = true,
            location = (string?)null,
            startTime = (string?)null,
            endTime = (string?)null,
            notes = (string?)null,
        });
        var pattern = await ReadAsync<PatternDto>(patternResp);

        await Client.PostAsJsonAsync("/api/member-agenda/exceptions", new
        {
            memberId = handle.Member.Id,
            date = date.ToString("yyyy-MM-dd"),
            patternId = pattern!.Id,
            isCancelled = true,
            label = (string?)null,
            location = (string?)null,
            startTime = (string?)null,
            endTime = (string?)null,
            transportMode = (string?)null,
            isAway = (bool?)null,
            notes = (string?)null,
        });

        var del = await Client.DeleteAsync($"/api/member-agenda/patterns/{pattern.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Both pattern and the override must be gone.
        var counts = await WithDbAsync(async db => new
        {
            Patterns = await db.MemberAgendaPatterns.CountAsync(),
            Exceptions = await db.MemberAgendaExceptions.CountAsync(),
        });
        counts.Patterns.Should().Be(0);
        counts.Exceptions.Should().Be(0);
    }

    private sealed record PatternDto(Guid Id);
    private sealed record OverviewDto(string From, string To, List<ResolvedDto> Resolved);
    private sealed record ResolvedDto(
        Guid MemberId,
        string Date,
        Guid? PatternId,
        Guid? ExceptionId,
        string Label,
        string? Location,
        string? StartTime,
        string? EndTime,
        string TransportMode,
        bool IsAway);
}
