using FamilyNido.Domain.Calendar;
using FamilyNido.Persistence;
using Google.Apis.Calendar.v3.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Synchronization engine for the calendar mirror. Talks to Google through
/// <see cref="GoogleCalendarClient"/>, upserts into <see cref="ApplicationDbContext"/>,
/// and persists the <c>nextSyncToken</c> so subsequent runs do an incremental delta.
/// Reused by both the periodic background service and the manual-sync endpoint.
/// </summary>
public sealed class CalendarSynchronizer
{
    private readonly ApplicationDbContext _db;
    private readonly GoogleOAuthService _oauth;
    private readonly GoogleCalendarClient _client;
    private readonly TimeProvider _clock;
    private readonly ILogger<CalendarSynchronizer> _logger;

    /// <summary>Primary constructor.</summary>
    public CalendarSynchronizer(
        ApplicationDbContext db,
        GoogleOAuthService oauth,
        GoogleCalendarClient client,
        TimeProvider clock,
        ILogger<CalendarSynchronizer> logger)
    {
        _db = db;
        _oauth = oauth;
        _client = client;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs a sync for every imported calendar of <paramref name="accountId"/>. Used
    /// by the manual-sync endpoint to trigger an account-scoped refresh on demand.
    /// </summary>
    public async Task SyncAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await _db.GoogleAccounts
            .Include(a => a.Calendars)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null || account.IsRevoked)
        {
            return;
        }

        await SyncAccountInternalAsync(account, cancellationToken);
    }

    /// <summary>
    /// Sweeps every healthy account and calendar. Called by the background timer.
    /// Errors on a single calendar are isolated so that one failing account does
    /// not block the rest.
    /// </summary>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        var accounts = await _db.GoogleAccounts
            .Include(a => a.Calendars)
            .Where(a => !a.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await SyncAccountInternalAsync(account, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync failed for Google account {AccountId} ({Email})", account.Id, account.Email);
                account.LastError = ex.Message;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task SyncAccountInternalAsync(GoogleAccount account, CancellationToken cancellationToken)
    {
        string refreshToken;
        try
        {
            refreshToken = _oauth.UnprotectRefreshToken(account.EncryptedRefreshToken);
        }
        catch (Exception)
        {
            account.IsRevoked = true;
            account.LastError = "Stored refresh token could not be decrypted; re-link required.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Probe the credential by refreshing once so we fail fast on revoked tokens
        // before iterating calendars.
        var refresh = await _oauth.RefreshAccessTokenAsync(refreshToken, cancellationToken);
        if (refresh is null)
        {
            account.IsRevoked = true;
            account.LastError = "Google rejected the refresh token (invalid_grant).";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        account.LastError = null;

        var imported = account.Calendars.Where(c => c.IsImported).ToList();
        foreach (var calendar in imported)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await SyncCalendarAsync(calendar, refreshToken, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncCalendarAsync(LinkedCalendar calendar, string refreshToken, CancellationToken cancellationToken)
    {
        GoogleEventsPage page;
        try
        {
            page = await _client.ListEventsAsync(
                refreshToken,
                calendar.ExternalCalendarId,
                calendar.SyncToken,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "events.list failed for calendar {CalendarId}", calendar.ExternalCalendarId);
            return;
        }

        if (page.FullSyncRequired)
        {
            // Sync token expired (Gone). Drop everything we had and let the next tick run a full sync.
            await _db.CalendarEvents
                .Where(e => e.LinkedCalendarId == calendar.Id)
                .ExecuteDeleteAsync(cancellationToken);
            calendar.SyncToken = null;
            calendar.LastSyncedAt = null;
            return;
        }

        var familyId = await ResolveFamilyIdAsync(calendar, cancellationToken);

        var existingByExternalId = await _db.CalendarEvents
            .Where(e => e.LinkedCalendarId == calendar.Id)
            .ToDictionaryAsync(e => e.ExternalEventId, StringComparer.Ordinal, cancellationToken);

        foreach (var googleEvent in page.Events)
        {
            ApplyEvent(googleEvent, calendar, familyId, existingByExternalId);
        }

        if (!string.IsNullOrEmpty(page.NextSyncToken))
        {
            calendar.SyncToken = page.NextSyncToken;
        }

        calendar.LastSyncedAt = _clock.GetUtcNow();
    }

    private async Task<Guid> ResolveFamilyIdAsync(LinkedCalendar calendar, CancellationToken cancellationToken)
    {
        // The calendar's owning account is the source of truth for FamilyId. Pull it
        // explicitly: relying on calendar.GoogleAccount being attached requires Include.
        if (calendar.GoogleAccount is { } loaded)
        {
            return loaded.FamilyId;
        }

        return await _db.GoogleAccounts
            .Where(a => a.Id == calendar.GoogleAccountId)
            .Select(a => a.FamilyId)
            .FirstAsync(cancellationToken);
    }

    private void ApplyEvent(
        Event googleEvent,
        LinkedCalendar calendar,
        Guid familyId,
        Dictionary<string, CalendarEvent> existing)
    {
        if (string.IsNullOrEmpty(googleEvent.Id))
        {
            return;
        }

        // status=cancelled means the event was deleted in Google. We mirror that by
        // removing the row.
        if (string.Equals(googleEvent.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            if (existing.TryGetValue(googleEvent.Id, out var doomed))
            {
                _db.CalendarEvents.Remove(doomed);
                existing.Remove(googleEvent.Id);
            }
            return;
        }

        if (!TryReadInstants(googleEvent, out var startAt, out var endAt, out var isAllDay, out var timeZone))
        {
            // Events without start/end are valid in Google's data model (e.g. tasks),
            // but the calendar UI cannot render them. Skip silently.
            return;
        }

        if (existing.TryGetValue(googleEvent.Id, out var row))
        {
            row.Title = googleEvent.Summary ?? "(sin título)";
            row.Description = googleEvent.Description;
            row.Location = googleEvent.Location;
            row.StartAt = startAt;
            row.EndAt = endAt;
            row.IsAllDay = isAllDay;
            row.OriginalTimeZone = timeZone;
            row.HtmlLink = googleEvent.HtmlLink;
            row.IcalUid = googleEvent.ICalUID;
        }
        else
        {
            _db.CalendarEvents.Add(new CalendarEvent
            {
                FamilyId = familyId,
                LinkedCalendarId = calendar.Id,
                ExternalEventId = googleEvent.Id,
                IcalUid = googleEvent.ICalUID,
                Title = googleEvent.Summary ?? "(sin título)",
                Description = googleEvent.Description,
                Location = googleEvent.Location,
                StartAt = startAt,
                EndAt = endAt,
                IsAllDay = isAllDay,
                OriginalTimeZone = timeZone,
                HtmlLink = googleEvent.HtmlLink,
            });
        }
    }

    private static bool TryReadInstants(
        Event googleEvent,
        out DateTimeOffset startAt,
        out DateTimeOffset endAt,
        out bool isAllDay,
        out string? timeZone)
    {
        startAt = default;
        endAt = default;
        isAllDay = false;
        timeZone = googleEvent.Start?.TimeZone ?? googleEvent.End?.TimeZone;

        if (googleEvent.Start is null || googleEvent.End is null)
        {
            return false;
        }

        if (googleEvent.Start.DateTimeDateTimeOffset is { } startDateTime &&
            googleEvent.End.DateTimeDateTimeOffset is { } endDateTime)
        {
            // Npgsql's timestamptz requires Offset=00:00, so normalize the original
            // event offset (Google echoes whatever the author had) to UTC.
            startAt = startDateTime.ToUniversalTime();
            endAt = endDateTime.ToUniversalTime();
            return true;
        }

        if (!string.IsNullOrEmpty(googleEvent.Start.Date) && !string.IsNullOrEmpty(googleEvent.End.Date))
        {
            // All-day events come as YYYY-MM-DD strings. Treat them as midnight in the
            // event's original timezone, falling back to UTC. End is exclusive (Google convention).
            isAllDay = true;
            var tz = TryFindTimeZone(timeZone) ?? TimeZoneInfo.Utc;
            startAt = ParseAllDay(googleEvent.Start.Date, tz).ToUniversalTime();
            endAt = ParseAllDay(googleEvent.End.Date, tz).ToUniversalTime();
            return true;
        }

        return false;
    }

    private static DateTimeOffset ParseAllDay(string yyyyMmDd, TimeZoneInfo tz)
    {
        var date = DateOnly.Parse(yyyyMmDd, System.Globalization.CultureInfo.InvariantCulture);
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static TimeZoneInfo? TryFindTimeZone(string? id)
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
