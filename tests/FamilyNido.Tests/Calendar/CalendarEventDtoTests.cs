using FamilyNido.Api.Features.Calendar;
using FamilyNido.Domain.Calendar;
using FluentAssertions;

namespace FamilyNido.Tests.Calendar;

/// <summary>
/// Issue #13: the DTO must surface a <c>StartDate</c> / <c>EndDate</c> pair on
/// all-day events so the SPA can render them as a calendar date without
/// re-applying the browser's timezone (which is what made Christmas land on
/// Dec 24 for users east… er, west of UTC).
/// </summary>
public sealed class CalendarEventDtoTests
{
    [Fact]
    public void All_day_event_in_new_york_keeps_christmas_on_december_25()
    {
        var ev = new CalendarEvent
        {
            FamilyId = Guid.NewGuid(),
            LinkedCalendarId = Guid.NewGuid(),
            ExternalEventId = "abc",
            Title = "Christmas Day",
            // Midnight EST = 05:00 UTC. This is what the synchronizer would
            // persist after the issue #13 fix.
            StartAt = new DateTimeOffset(2026, 12, 25, 5, 0, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 12, 26, 5, 0, 0, TimeSpan.Zero),
            IsAllDay = true,
            OriginalTimeZone = "America/New_York",
        };

        var dto = CalendarEventDto.From(ev);

        dto.StartDate.Should().Be("2026-12-25");
        dto.EndDate.Should().Be("2026-12-26");
        dto.IsAllDay.Should().BeTrue();
    }

    [Fact]
    public void All_day_event_in_madrid_keeps_the_calendar_date_stable()
    {
        var ev = new CalendarEvent
        {
            FamilyId = Guid.NewGuid(),
            LinkedCalendarId = Guid.NewGuid(),
            ExternalEventId = "xyz",
            Title = "Cumpleaños Aaron",
            // 22:00 UTC on Jun 5 = midnight CEST on Jun 6.
            StartAt = new DateTimeOffset(2026, 6, 5, 22, 0, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 6, 6, 22, 0, 0, TimeSpan.Zero),
            IsAllDay = true,
            OriginalTimeZone = "Europe/Madrid",
        };

        var dto = CalendarEventDto.From(ev);

        dto.StartDate.Should().Be("2026-06-06");
        dto.EndDate.Should().Be("2026-06-07");
    }

    [Fact]
    public void Timed_event_does_not_emit_a_start_date_or_end_date()
    {
        var ev = new CalendarEvent
        {
            FamilyId = Guid.NewGuid(),
            LinkedCalendarId = Guid.NewGuid(),
            ExternalEventId = "timed",
            Title = "Daily standup",
            StartAt = new DateTimeOffset(2026, 5, 18, 7, 30, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 5, 18, 8, 0, 0, TimeSpan.Zero),
            IsAllDay = false,
            OriginalTimeZone = "Europe/Madrid",
        };

        var dto = CalendarEventDto.From(ev);

        dto.StartDate.Should().BeNull();
        dto.EndDate.Should().BeNull();
        dto.IsAllDay.Should().BeFalse();
    }

    [Fact]
    public void All_day_event_with_missing_original_timezone_falls_back_to_utc()
    {
        // Legacy rows from before the issue #13 fix may have stored OriginalTimeZone
        // as null. The DTO should not throw — it falls back to UTC interpretation,
        // which is the same (broken-for-NY) shape callers saw before the fix.
        // Once those rows resync from Google they'll be corrected.
        var ev = new CalendarEvent
        {
            FamilyId = Guid.NewGuid(),
            LinkedCalendarId = Guid.NewGuid(),
            ExternalEventId = "legacy",
            Title = "Old all-day",
            StartAt = new DateTimeOffset(2026, 12, 25, 0, 0, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 12, 26, 0, 0, 0, TimeSpan.Zero),
            IsAllDay = true,
            OriginalTimeZone = null,
        };

        var dto = CalendarEventDto.From(ev);

        dto.StartDate.Should().Be("2026-12-25");
        dto.EndDate.Should().Be("2026-12-26");
    }
}
