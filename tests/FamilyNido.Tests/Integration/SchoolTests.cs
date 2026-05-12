using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for /api/school: profile upsert, holidays, weekly day-schedule
/// patterns and per-date exceptions, plus the overview resolver that merges
/// them into resolved per-day rows.
/// </summary>
public sealed class SchoolTests : IntegrationTestBase
{
    public SchoolTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Holiday_added_then_listed_in_overview()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var add = await Client.PostAsJsonAsync("/api/school/holidays", new
        {
            startDate = today.ToString("yyyy-MM-dd"),
            endDate = today.AddDays(2).ToString("yyyy-MM-dd"),
            label = "Festivo de prueba",
        });
        add.IsSuccessStatusCode.Should().BeTrue();

        var resp = await Client.GetAsync(
            $"/api/school/overview?from={today:yyyy-MM-dd}&to={today.AddDays(2):yyyy-MM-dd}");
        var overview = await ReadAsync<OverviewDto>(resp);
        overview!.Holidays.Should().Contain(h => h.Label == "Festivo de prueba");
    }

    [Fact]
    public async Task Day_schedule_pattern_persists_for_a_kid()
    {
        var handle = await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        // A child member to schedule.
        var bob = await WithDbAsync(db => TestSeed.SeedMemberAsync(db, handle.Family.Id, "Bob"));

        var resp = await Client.PutAsJsonAsync(
            $"/api/school/members/{bob.Id}/day-schedule",
            new
            {
                slots = new[]
                {
                    new
                    {
                        dayOfWeek = "Monday",
                        dropoffMemberId = (Guid?)handle.Member.Id,
                        pickupMemberId = (Guid?)null,
                    },
                },
            });
        resp.IsSuccessStatusCode.Should().BeTrue();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monday = today;
        for (var i = 0; i < 14; i++)
        {
            if (today.AddDays(i).DayOfWeek == DayOfWeek.Monday)
            {
                monday = today.AddDays(i);
                break;
            }
        }
        var ovResp = await Client.GetAsync(
            $"/api/school/overview?from={monday:yyyy-MM-dd}&to={monday:yyyy-MM-dd}");
        var ov = await ReadAsync<OverviewDto>(ovResp);
        ov!.ResolvedDays.Should().Contain(d => d.MemberId == bob.Id && d.DropoffMemberId == handle.Member.Id);
    }

    private sealed record OverviewDto(
        List<HolidayDto> Holidays,
        List<ResolvedDayDto> ResolvedDays);
    private sealed record HolidayDto(Guid Id, string StartDate, string EndDate, string Label);
    private sealed record ResolvedDayDto(
        Guid MemberId,
        string Date,
        Guid? DropoffMemberId,
        Guid? PickupMemberId);
}
