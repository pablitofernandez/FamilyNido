using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Slice: kick off the Google OAuth flow for the current user. Returns the URL the
/// frontend should redirect to plus the encrypted state payload that has to be set
/// in a short-lived cookie alongside the redirect.
/// </summary>
public static class StartGoogleLink
{
    /// <summary>Empty command — the caller is identified via <see cref="ICurrentUserContext"/>.</summary>
    public sealed record Command() : IRequest<Result<Response>>;

    /// <summary>Response returned to the frontend.</summary>
    /// <param name="AuthUrl">Full Google authorization URL the user has to follow.</param>
    /// <param name="EncryptedState">Encrypted state to set in the OAuth state cookie.</param>
    /// <param name="ExpiresAt">UTC instant the state expires (cookie max-age cap).</param>
    public sealed record Response(string AuthUrl, string EncryptedState, DateTimeOffset ExpiresAt);

    /// <summary>Handler — composes the auth URL via <see cref="GoogleOAuthService"/>.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly ICurrentUserContext _userContext;
        private readonly GoogleOAuthService _oauth;

        /// <summary>Primary constructor.</summary>
        public Handler(ICurrentUserContext userContext, GoogleOAuthService oauth)
        {
            _userContext = userContext;
            _oauth = oauth;
        }

        /// <inheritdoc />
        public async Task<Result<Response>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var (authUrl, encryptedState, expiresAt) = _oauth.BuildAuthorizationRequest(current.User.Id);
            return new Response(authUrl, encryptedState, expiresAt);
        }
    }
}
