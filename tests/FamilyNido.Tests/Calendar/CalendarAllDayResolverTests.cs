using FamilyNido.Api.Features.Calendar;
using FluentAssertions;

namespace FamilyNido.Tests.Calendar;

/// <summary>
/// Issue #13 regression coverage: all-day Google Calendar events were being
/// stored as midnight UTC instead of midnight in the family's timezone, so a
/// family in America/New_York saw Christmas Day on Dec 24.
/// </summary>
public sealed class CalendarAllDayResolverTests
{
    [Fact]
    public void Falls_back_to_family_timezone_when_google_does_not_send_one()
    {
        // Google's start.date for an all-day Christmas in 2026, no Start.TimeZone
        // (the typical shape for the built-in US Holidays calendar).
        var (start, end, tz) = CalendarAllDayResolver.Resolve(
            startDate: "2026-12-25",
            endDate: "2026-12-26",
            googleTimeZone: null,
            familyTimeZone: "America/New_York");

        // Midnight EST = 05:00 UTC (EST is UTC-5).
        start.Should().Be(new DateTimeOffset(2026, 12, 25, 5, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2026, 12, 26, 5, 0, 0, TimeSpan.Zero));
        tz.Should().Be("America/New_York");
    }

    [Fact]
    public void Uses_google_timezone_when_provided()
    {
        var (start, _, tz) = CalendarAllDayResolver.Resolve(
            startDate: "2026-06-15",
            endDate: "2026-06-16",
            googleTimeZone: "Europe/Madrid",
            familyTimeZone: "America/New_York");

        // June 15 midnight Madrid = 22:00 UTC on June 14 (Madrid is UTC+2 in summer).
        start.Should().Be(new DateTimeOffset(2026, 6, 14, 22, 0, 0, TimeSpan.Zero));
        tz.Should().Be("Europe/Madrid");
    }

    [Fact]
    public void Falls_back_to_utc_when_both_timezones_are_unknown()
    {
        var (start, _, tz) = CalendarAllDayResolver.Resolve(
            startDate: "2026-06-15",
            endDate: "2026-06-16",
            googleTimeZone: "Mars/Olympus",
            familyTimeZone: "Vulcan/ShiKahr");

        start.Should().Be(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        tz.Should().Be(TimeZoneInfo.Utc.Id);
    }

    [Fact]
    public void Format_local_date_returns_calendar_date_in_the_supplied_timezone()
    {
        // 05:00 UTC = midnight EST.
        var instant = new DateTimeOffset(2026, 12, 25, 5, 0, 0, TimeSpan.Zero);
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        CalendarAllDayResolver.FormatLocalDate(instant, ny)
            .Should().Be("2026-12-25");
    }

    [Fact]
    public void Format_local_date_does_not_drift_for_european_timezones()
    {
        // 22:00 UTC on Jun 14 = midnight CEST Jun 15.
        var instant = new DateTimeOffset(2026, 6, 14, 22, 0, 0, TimeSpan.Zero);
        var madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

        CalendarAllDayResolver.FormatLocalDate(instant, madrid)
            .Should().Be("2026-06-15");
    }
}
