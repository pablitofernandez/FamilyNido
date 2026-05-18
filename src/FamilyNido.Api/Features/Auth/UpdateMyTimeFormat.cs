using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Slice for <c>PUT /api/auth/me/time-format</c>. Sets — or clears — the
/// caller's explicit override for the 12H/24H clock the SPA renders.
/// Sending a <c>null</c> body field resets to "auto" (the SPA falls back
/// to the active i18n bundle's native hour cycle).
/// </summary>
public static class UpdateMyTimeFormat
{
    /// <summary>Command body carrying the new preference (or null to clear).</summary>
    /// <param name="TimeFormat">New override, or <c>null</c> to reset to auto.</param>
    public sealed record Command(TimeFormatPreference? TimeFormat) : IRequest<Result<Response>>;

    /// <summary>Echo of the persisted value, used by the frontend to confirm.</summary>
    /// <param name="TimeFormat">The new override stored on the user (or null).</param>
    public sealed record Response(TimeFormatPreference? TimeFormat);

    /// <summary>
    /// Validator: enums on the wire deserialize from string via the global
    /// <c>JsonStringEnumConverter</c>. Unknown values throw before we get
    /// here, so the validator only needs to enforce the nullable contract
    /// (which is trivial — any value, including null, is acceptable).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        // No rules needed: null = "auto", any defined enum value is fine.
    }

    /// <summary>Persists the new override on the caller's <c>User</c> row.</summary>
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

            // Re-fetch tracked so SaveChanges flushes the column update.
            var tracked = await _db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
            if (tracked is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "User not found.");
            }

            tracked.TimeFormat = request.TimeFormat;
            await _db.SaveChangesAsync(cancellationToken);

            return new Response(request.TimeFormat);
        }
    }
}
