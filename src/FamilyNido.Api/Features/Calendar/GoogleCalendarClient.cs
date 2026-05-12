using System.Globalization;
using FamilyNido.Api.Options;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Thin wrapper around the official <c>Google.Apis.Calendar.v3</c> SDK that hides
/// the credential plumbing. Each call accepts a refresh token and builds a fresh
/// in-memory <see cref="UserCredential"/> — no on-disk token cache, no global state.
/// </summary>
public sealed class GoogleCalendarClient
{
    private readonly IOptions<CalendarOptions> _options;

    /// <summary>Primary constructor.</summary>
    public GoogleCalendarClient(IOptions<CalendarOptions> options)
    {
        _options = options;
    }

    /// <summary>Lists every calendar visible to the linked Google account.</summary>
    /// <param name="refreshToken">Plaintext refresh token of the linked account.</param>
    /// <param name="cancellationToken">Cancellation propagated from the caller.</param>
    /// <returns>Calendars as Google reports them — caller decides which to mirror.</returns>
    public async Task<IReadOnlyList<CalendarListEntry>> ListCalendarsAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var service = BuildService(refreshToken);
        var request = service.CalendarList.List();
        request.MinAccessRole = CalendarListResource.ListRequest.MinAccessRoleEnum.Reader;

        var aggregated = new List<CalendarListEntry>();
        string? pageToken = null;
        do
        {
            request.PageToken = pageToken;
            var page = await request.ExecuteAsync(cancellationToken);
            if (page.Items is not null)
            {
                aggregated.AddRange(page.Items);
            }
            pageToken = page.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return aggregated;
    }

    /// <summary>
    /// Pages through <c>events.list</c> for a single calendar. When
    /// <paramref name="syncToken"/> is non-null, performs an incremental sync; when
    /// null, performs a full sync within the configured lookback/lookahead window.
    /// </summary>
    /// <param name="refreshToken">Plaintext refresh token of the linked account.</param>
    /// <param name="externalCalendarId">Google calendar id to enumerate.</param>
    /// <param name="syncToken">Latest <c>nextSyncToken</c> received, or null for a full sync.</param>
    /// <param name="cancellationToken">Cancellation propagated from the caller.</param>
    public async Task<GoogleEventsPage> ListEventsAsync(
        string refreshToken,
        string externalCalendarId,
        string? syncToken,
        CancellationToken cancellationToken)
    {
        using var service = BuildService(refreshToken);

        var aggregated = new List<Event>();
        string? pageToken = null;
        string? nextSyncToken = null;
        var fullSyncRequired = false;

        do
        {
            var request = service.Events.List(externalCalendarId);
            request.PageToken = pageToken;
            // Critical: SingleEvents=true makes Google expand recurrences into individual instances,
            // so the FamilyNido sync engine never has to deal with RRULE expansion.
            request.SingleEvents = true;
            request.ShowDeleted = !string.IsNullOrEmpty(syncToken); // incremental sync needs deletes
            request.MaxResults = 250;

            if (!string.IsNullOrEmpty(syncToken))
            {
                request.SyncToken = syncToken;
            }
            else
            {
                var now = DateTime.UtcNow;
                request.TimeMinDateTimeOffset = now.AddTicks(-_options.Value.FullSyncLookback.Ticks);
                request.TimeMaxDateTimeOffset = now.AddTicks(_options.Value.FullSyncLookahead.Ticks);
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            }

            try
            {
                var page = await request.ExecuteAsync(cancellationToken);
                if (page.Items is not null)
                {
                    aggregated.AddRange(page.Items);
                }
                pageToken = page.NextPageToken;
                nextSyncToken = page.NextSyncToken ?? nextSyncToken;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Gone)
            {
                // 410 Gone = the sync token has expired; signal the caller to drop it
                // and run a full sync next iteration.
                fullSyncRequired = true;
                break;
            }
        } while (!string.IsNullOrEmpty(pageToken));

        return new GoogleEventsPage(aggregated, nextSyncToken, fullSyncRequired);
    }

    private CalendarService BuildService(string refreshToken)
    {
        var options = _options.Value;
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = options.GoogleClientId,
                ClientSecret = options.GoogleClientSecret,
            },
            Scopes = [CalendarService.Scope.CalendarReadonly],
            DataStore = new NullDataStore(),
        });

        var token = new TokenResponse
        {
            RefreshToken = refreshToken,
            // Force the SDK to fetch a fresh access token on first use.
            ExpiresInSeconds = 0,
            IssuedUtc = DateTime.UtcNow.AddDays(-1),
        };

        var credential = new UserCredential(flow, "user", token);

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "FamilyNido",
        });
    }

    /// <summary>
    /// In-memory "data store" so the SDK doesn't try to persist tokens on disk.
    /// We hold tokens ourselves (encrypted in Postgres) and rebuild the credential
    /// per request.
    /// </summary>
    private sealed class NullDataStore : IDataStore
    {
        public Task ClearAsync() => Task.CompletedTask;
        public Task DeleteAsync<T>(string key) => Task.CompletedTask;
        public Task<T> GetAsync<T>(string key) => Task.FromResult<T>(default!);
        public Task StoreAsync<T>(string key, T value) => Task.CompletedTask;
    }
}

/// <summary>
/// Result of a <c>events.list</c> page traversal.
/// </summary>
/// <param name="Events">All events from every page returned by Google.</param>
/// <param name="NextSyncToken">Token to persist for the next incremental sync; null when a full sync is still pending.</param>
/// <param name="FullSyncRequired">True when Google replied 410 Gone — the caller must reset its stored sync token and run a full sync next.</param>
public sealed record GoogleEventsPage(
    IReadOnlyList<Event> Events,
    string? NextSyncToken,
    bool FullSyncRequired);
