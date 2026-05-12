using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Calendar;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Slice: process the Google OAuth callback. Validates the state cookie, exchanges
/// the authorization code for tokens, persists (or refreshes) the
/// <see cref="GoogleAccount"/>, and discovers the visible calendars so the user can
/// choose which to import on the cuentas screen.
/// </summary>
public static class HandleGoogleCallback
{
    /// <summary>Inputs harvested from the callback request.</summary>
    /// <param name="Code">Authorization code returned by Google.</param>
    /// <param name="QueryStateNonce">State value Google echoed back via the <c>state</c> query parameter.</param>
    /// <param name="EncryptedStateCookie">Encrypted payload from the OAuth state cookie.</param>
    public sealed record Command(
        string Code,
        string? QueryStateNonce,
        string? EncryptedStateCookie) : IRequest<Result<Response>>;

    /// <summary>Outcome surfaced to the redirect handler.</summary>
    /// <param name="GoogleAccountId">Id of the persisted account row.</param>
    /// <param name="DiscoveredCalendars">How many calendars were discovered (purely informational).</param>
    public sealed record Response(Guid GoogleAccountId, int DiscoveredCalendars);

    /// <summary>Handler — runs the OAuth post-back logic.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly ApplicationDbContext _db;
        private readonly GoogleOAuthService _oauth;
        private readonly GoogleCalendarClient _calendarClient;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            GoogleOAuthService oauth,
            GoogleCalendarClient calendarClient)
        {
            _db = db;
            _oauth = oauth;
            _calendarClient = calendarClient;
        }

        /// <inheritdoc />
        public async Task<Result<Response>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var state = _oauth.ValidateState(request.EncryptedStateCookie, request.QueryStateNonce);
            if (state is null)
            {
                return ApplicationError.Validation(
                    "calendar.oauth_state_invalid",
                    "El estado OAuth es inválido o ha expirado. Inicia el proceso de vinculación de nuevo.");
            }

            var user = await _db.Users
                .Include(u => u.FamilyMember)
                .FirstOrDefaultAsync(u => u.Id == state.UserId, cancellationToken);

            if (user is null)
            {
                return ApplicationError.NotFound(
                    "calendar.user_not_found",
                    "El usuario que inició la vinculación ya no existe.");
            }

            if (user.FamilyMember is null)
            {
                return ApplicationError.Forbidden(
                    "calendar.user_not_linked",
                    "El usuario debe estar enlazado a un miembro familiar para vincular Google Calendar.");
            }

            GoogleTokenResponse tokens;
            try
            {
                tokens = await _oauth.ExchangeCodeAsync(request.Code, cancellationToken);
            }
            catch (Exception)
            {
                return ApplicationError.Validation(
                    "calendar.token_exchange_failed",
                    "Google rechazó el intercambio de código. Inténtalo de nuevo.");
            }

            if (string.IsNullOrEmpty(tokens.RefreshToken) || string.IsNullOrEmpty(tokens.IdToken))
            {
                return ApplicationError.Validation(
                    "calendar.missing_refresh_token",
                    "Google no devolvió un refresh token. Revoca el acceso en tu cuenta y vuelve a intentarlo.");
            }

            (string Email, string? Name) identity;
            try
            {
                identity = GoogleOAuthService.DecodeIdToken(tokens.IdToken);
            }
            catch (Exception)
            {
                return ApplicationError.Validation(
                    "calendar.invalid_id_token",
                    "Google devolvió un id_token con formato inesperado.");
            }

            // Either reuse an existing link (refresh token rotation) or insert a new one.
            var account = await _db.GoogleAccounts
                .Include(a => a.Calendars)
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.Email == identity.Email, cancellationToken);

            if (account is null)
            {
                account = new GoogleAccount
                {
                    FamilyId = user.FamilyMember.FamilyId,
                    UserId = user.Id,
                    Email = identity.Email,
                    DisplayName = identity.Name,
                    EncryptedRefreshToken = _oauth.ProtectRefreshToken(tokens.RefreshToken),
                    IsRevoked = false,
                    LastError = null,
                };
                _db.GoogleAccounts.Add(account);
            }
            else
            {
                account.EncryptedRefreshToken = _oauth.ProtectRefreshToken(tokens.RefreshToken);
                account.DisplayName = identity.Name;
                account.IsRevoked = false;
                account.LastError = null;
            }

            // Discover calendars now so the cuentas UI has something to render right
            // after the redirect. New calendars are added; pre-existing ones keep their
            // IsImported / FamilyMemberId / SyncToken state.
            IReadOnlyList<Google.Apis.Calendar.v3.Data.CalendarListEntry> discovered;
            try
            {
                discovered = await _calendarClient.ListCalendarsAsync(tokens.RefreshToken, cancellationToken);
            }
            catch (Exception ex)
            {
                account.LastError = $"calendarList.list failed: {ex.Message}";
                await _db.SaveChangesAsync(cancellationToken);
                return ApplicationError.Validation(
                    "calendar.list_failed",
                    "No se pudo recuperar la lista de calendarios. Revisa los permisos otorgados a la app.");
            }

            var existingByExternalId = account.Calendars
                .ToDictionary(c => c.ExternalCalendarId, StringComparer.Ordinal);

            foreach (var entry in discovered)
            {
                if (string.IsNullOrEmpty(entry.Id))
                {
                    continue;
                }

                if (existingByExternalId.TryGetValue(entry.Id, out var existing))
                {
                    existing.Summary = entry.Summary ?? existing.Summary;
                    existing.Description = entry.Description ?? existing.Description;
                    existing.ColorHex = entry.BackgroundColor ?? existing.ColorHex;
                }
                else
                {
                    account.Calendars.Add(new LinkedCalendar
                    {
                        GoogleAccountId = account.Id,
                        ExternalCalendarId = entry.Id,
                        Summary = entry.Summary ?? entry.Id,
                        Description = entry.Description,
                        ColorHex = entry.BackgroundColor,
                        IsImported = false,
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new Response(account.Id, account.Calendars.Count);
        }
    }
}
