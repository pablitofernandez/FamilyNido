using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Slice for <c>PUT /api/auth/me/preferred-language</c>. Persists the caller's
/// chosen UI language so digest emails, mention notifications and integration-
/// generated task titles match the bundle they're using on the frontend.
/// </summary>
/// <remarks>
/// The whitelist of accepted tags lives here on purpose — when a new locale
/// is added (frontend bundle + xlf + backend strings) we want the API to
/// reject older or unsupported tags loudly instead of silently storing
/// garbage that no localizer maps.
/// </remarks>
public static class UpdateMyPreferredLanguage
{
    /// <summary>Tags accepted by the validator. Keep aligned with the Angular bundles.</summary>
    public static readonly string[] AllowedTags = { "es-ES", "en-US" };

    /// <summary>Command body carrying the new tag.</summary>
    /// <param name="Language">BCP-47 tag from <see cref="AllowedTags"/>.</param>
    public sealed record Command(string Language) : IRequest<Result<Response>>;

    /// <summary>Echo of the persisted value, used by the frontend to confirm.</summary>
    /// <param name="Language">The new tag stored on the user.</param>
    public sealed record Response(string Language);

    /// <summary>Validator: ensures the requested tag is one we actually support.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator.</summary>
        public Validator()
        {
            RuleFor(x => x.Language)
                .NotEmpty()
                .Must(value => Array.IndexOf(AllowedTags, value) >= 0)
                .WithMessage("Unsupported language tag.");
        }
    }

    /// <summary>Persists the new preferred language on the caller's <c>User</c> row.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext)
        {
            _db = db;
            _userContext = userContext;
        }

        /// <inheritdoc />
        public async Task<Result<Response>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var user = await _userContext.GetUserAsync(cancellationToken);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Not signed in.");
            }

            // The handler only mutates a single column. We re-fetch by id so the
            // SaveChanges below operates on a tracked entity inside this scope —
            // GetUserAsync may have returned a no-tracking projection.
            var tracked = await _db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
            if (tracked is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "User not found.");
            }

            tracked.PreferredLanguage = request.Language;
            await _db.SaveChangesAsync(cancellationToken);

            return new Response(request.Language);
        }
    }
}
