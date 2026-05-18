using System.Globalization;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Turns Google's <c>YYYY-MM-DD</c> all-day date strings into the
/// <see cref="DateTimeOffset"/> pair we store on <c>CalendarEvent</c>. Lives in
/// its own class (separate from <see cref="CalendarSynchronizer"/>) so the logic
/// can be unit-tested without setting up the Google client or a DbContext.
/// </summary>
internal static class CalendarAllDayResolver
{
    /// <summary>
    /// Compute the UTC start/end instants and the IANA id we should record as
    /// <c>OriginalTimeZone</c> for an all-day event.
    /// </summary>
    /// <param name="startDate">Inclusive start (<c>YYYY-MM-DD</c>).</param>
    /// <param name="endDate">Exclusive end (Google convention; <c>YYYY-MM-DD</c>).</param>
    /// <param name="googleTimeZone">
    /// IANA id Google reported on the event, if any. All-day events typically come
    /// without one — Google leaves it null.
    /// </param>
    /// <param name="familyTimeZone">
    /// IANA id stored on <c>Family.TimeZone</c>. Used as the fallback when the
    /// event itself has no timezone. This is the critical bit: parsing
    /// <c>"2026-12-25"</c> as midnight UTC for a family in <c>America/New_York</c>
    /// shifts the displayed date one day earlier — using the family TZ keeps
    /// Christmas Day on December 25 everywhere a family member views it.
    /// </param>
    /// <returns>
    /// <c>StartUtc</c>, <c>EndUtc</c>, and the IANA id we actually used to
    /// interpret the dates — caller persists that as <c>OriginalTimeZone</c>
    /// so the DTO projection can render the date back to the same calendar day.
    /// </returns>
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc, string TimeZoneId) Resolve(
        string startDate,
        string endDate,
        string? googleTimeZone,
        string familyTimeZone)
    {
        var tz = CalendarTimeZones.TryFind(googleTimeZone)
                 ?? CalendarTimeZones.TryFind(familyTimeZone)
                 ?? TimeZoneInfo.Utc;

        return (
            ParseAsMidnightUtc(startDate, tz),
            ParseAsMidnightUtc(endDate, tz),
            tz.Id);
    }

    /// <summary>Render the date portion of <paramref name="instant"/> as <c>YYYY-MM-DD</c> in <paramref name="timeZone"/>.</summary>
    public static string FormatLocalDate(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseAsMidnightUtc(string yyyyMmDd, TimeZoneInfo tz)
    {
        var date = DateOnly.Parse(yyyyMmDd, CultureInfo.InvariantCulture);
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset).ToUniversalTime();
    }
}
