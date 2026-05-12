namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Payload stored in the encrypted state cookie during the Google OAuth dance.
/// Persisted only for the few minutes between the user clicking "link" and Google
/// redirecting back. Bound to the authenticated user so an attacker cannot reuse
/// a callback for a different identity.
/// </summary>
/// <param name="UserId">FamilyNido user id that initiated the link.</param>
/// <param name="Nonce">Random value also passed to Google as the <c>state</c> query parameter — must round-trip exactly.</param>
/// <param name="ExpiresAt">Hard expiration; callbacks beyond this are rejected.</param>
public sealed record GoogleOAuthState(Guid UserId, string Nonce, DateTimeOffset ExpiresAt);
