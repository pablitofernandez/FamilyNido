namespace FamilyNido.Api.Options;

/// <summary>
/// Configuration for the Google Calendar integration. The OAuth credentials are
/// shared across the family instance — every adult who links their Google account
/// reuses the same client. Refresh tokens are persisted per-account, encrypted by
/// ASP.NET Core Data Protection.
/// </summary>
public sealed class CalendarOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Calendar";

    /// <summary>OAuth client id from Google Cloud Console.</summary>
    public string GoogleClientId { get; init; } = string.Empty;

    /// <summary>OAuth client secret from Google Cloud Console.</summary>
    public string GoogleClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Public URL of the OAuth callback. Must match exactly the "Authorized redirect
    /// URIs" entry registered for the OAuth client. In dev typically
    /// <c>https://localhost:5001/api/calendar/google/callback</c>; in prod
    /// <c>https://familia.example.com/api/calendar/google/callback</c>.
    /// </summary>
    public string OAuthRedirectUri { get; init; } = string.Empty;

    /// <summary>
    /// Frontend route the user lands on after the OAuth dance, regardless of success
    /// or failure. Defaults to <c>/calendario/cuentas</c> so the linking UI re-renders
    /// with the new account (or an error banner).
    /// </summary>
    public string PostAuthRedirectPath { get; init; } = "/calendario/cuentas";

    /// <summary>
    /// Period of the background sync job. Defaults to 15 minutes per RF-CAL-004; can
    /// be lowered in dev to validate the pipeline faster.
    /// </summary>
    public TimeSpan SyncInterval { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Lower bound (relative to now) for events imported during a full sync. Anything
    /// older is skipped — a long-lived Google account would otherwise pull thousands
    /// of stale events on first sync. Defaults to 90 days back.
    /// </summary>
    public TimeSpan FullSyncLookback { get; init; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Upper bound (relative to now) for events imported during a full sync. Defaults
    /// to 365 days forward — Google's incremental sync will keep up after that.
    /// </summary>
    public TimeSpan FullSyncLookahead { get; init; } = TimeSpan.FromDays(365);
}
