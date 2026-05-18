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

        var (familyId, familyTimeZone) = await ResolveFamilyContextAsync(calendar, cancellationToken);

        var existingByExternalId = await _db.CalendarEvents
            .Where(e => e.LinkedCalendarId == calendar.Id)
            .ToDictionaryAsync(e => e.ExternalEventId, StringComparer.Ordinal, cancellationToken);

        foreach (var googleEvent in page.Events)
        {
            ApplyEvent(googleEvent, calendar, familyId, familyTimeZone, existingByExternalId);
        }

        if (!string.IsNullOrEmpty(page.NextSyncToken))
        {
            calendar.SyncToken = page.NextSyncToken;
        }

        calendar.LastSyncedAt = _clock.GetUtcNow();
    }

    private async Task<(Guid familyId, string familyTimeZone)> ResolveFamilyContextAsync(
        LinkedCalendar calendar,
        CancellationToken cancellationToken)
    {
        // FamilyId + TimeZone come together so the all-day fallback works (issue
        // #13) — parsing date-only events as midnight UTC shifts them one day for
        // any family west of UTC. One round trip on the GoogleAccount → Family
        // join is cheap; per-calendar, not per-event.
        return await _db.GoogleAccounts
            .Where(a => a.Id == calendar.GoogleAccountId)
            .Select(a => new ValueTuple<Guid, string>(a.FamilyId, a.Family!.TimeZone))
            .FirstAsync(cancellationToken);
    }

    private void ApplyEvent(
        Event googleEvent,
        LinkedCalendar calendar,
        Guid familyId,
        string familyTimeZone,
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

        if (!TryReadInstants(googleEvent, familyTimeZone, out var startAt, out var endAt, out var isAllDay, out var timeZone))
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
        string familyTimeZone,
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
            // All-day events come as YYYY-MM-DD strings, almost always without
            // a timezone (Google leaves Start.TimeZone null for date-only
            // events). Falling back to UTC made Christmas land on Dec 24 for a
            // family in America/New_York (issue #13); falling back to the
            // family's timezone keeps the date stable for everyone in that
            // household. End is exclusive — that's Google's convention, mirror
            // it verbatim.
            isAllDay = true;
            var (startUtc, endUtc, effectiveTz) = CalendarAllDayResolver.Resolve(
                googleEvent.Start.Date,
                googleEvent.End.Date,
                timeZone,
                familyTimeZone);
            startAt = startUtc;
            endAt = endUtc;
            // Persist the timezone we actually used so the DTO projection can
            // recover the original calendar date without guessing.
            timeZone = effectiveTz;
            return true;
        }

        return false;
    }
}
