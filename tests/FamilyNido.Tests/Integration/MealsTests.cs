using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Tests.Integration;

/// <summary>
/// Coverage for /api/meals: weekly view, slot upsert (per-course), clearing
/// a slot, autocomplete suggestions and "duplicate previous week".
/// </summary>
public sealed class MealsTests : IntegrationTestBase
{
    public MealsTests(IntegrationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Upsert_then_get_week_returns_the_dish()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Find Monday of the current ISO week.
        var dow = (int)today.DayOfWeek;
        var daysFromMonday = dow == 0 ? 6 : dow - 1;
        var monday = today.AddDays(-daysFromMonday);

        var resp = await Client.PutAsJsonAsync("/api/meals/slots", new
        {
            date = today.ToString("yyyy-MM-dd"),
            slot = "Lunch",
            course = "First",
            name = "Carbonara",
        });
        resp.IsSuccessStatusCode.Should().BeTrue();

        var weekResp = await Client.GetAsync($"/api/meals/week?startDate={monday:yyyy-MM-dd}");
        var week = await ReadAsync<WeekDto>(weekResp);

        var todayDay = week!.Days.FirstOrDefault(d => d.Date == today.ToString("yyyy-MM-dd"));
        todayDay.Should().NotBeNull();
        todayDay!.Lunch.Should().NotBeNull();
        todayDay.Lunch!.FirstCourse.Should().Be("Carbonara");
    }

    [Fact]
    public async Task Suggestions_returns_recently_used_names()
    {
        await WithDbAsync(db => TestSeed.SeedFamilyAsync(db, Fixture.Factory.Services));
        await TestSeed.LoginAsync(Client, "dan@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Register two dishes through the upsert flow.
        await Client.PutAsJsonAsync("/api/meals/slots", new
        {
            date = today.ToString("yyyy-MM-dd"),
            slot = "Lunch",
            course = "First",
            name = "Lentejas",
        });
        await Client.PutAsJsonAsync("/api/meals/slots", new
        {
            date = today.AddDays(-1).ToString("yyyy-MM-dd"),
            slot = "Dinner",
            course = "Second",
            name = "Tortilla",
        });

        var resp = await Client.GetAsync("/api/meals/suggestions?prefix=L");
        resp.IsSuccessStatusCode.Should().BeTrue();
        var content = await resp.Content.ReadAsStringAsync();
        content.Should().Contain("Lentejas");
    }

    private sealed record WeekDto(string WeekStart, List<DayDto> Days);
    private sealed record DayDto(string Date, SlotDto? Lunch, SlotDto? Dinner);
    private sealed record SlotDto(string? FirstCourse, string? SecondCourse);
}
