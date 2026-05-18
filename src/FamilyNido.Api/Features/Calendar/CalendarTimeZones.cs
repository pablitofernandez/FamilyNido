namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Small helper around <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> shared by
/// the synchronizer (writes) and the DTO projection (reads). Returns <c>null</c> on
/// unknown ids instead of throwing — calendar data comes from Google and we don't
/// want a bad IANA string to crash a sync or a read.
/// </summary>
internal static class CalendarTimeZones
{
    /// <summary>Resolve an IANA timezone id. Returns <c>null</c> if it's missing or unknown.</summary>
    public static TimeZoneInfo? TryFind(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return null;
        }
    }
}
