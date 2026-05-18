using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Slice for <c>PUT /api/auth/me/temperature-unit</c>. Sets — or clears —
/// the caller's explicit Celsius/Fahrenheit override for the weather
/// widget. Sending <c>null</c> resets to "auto" (derived from the active
/// i18n bundle on the SPA).
/// </summary>
public static class UpdateMyTemperatureUnit
{
    /// <summary>Command body carrying the new preference (or null to clear).</summary>
    /// <param name="TemperatureUnit">New override, or <c>null</c> to reset to auto.</param>
    public sealed record Command(TemperatureUnitPreference? TemperatureUnit) : IRequest<Result<Response>>;

    /// <summary>Echo of the persisted value, used by the frontend to confirm.</summary>
    /// <param name="TemperatureUnit">The new override stored on the user (or null).</param>
    public sealed record Response(TemperatureUnitPreference? TemperatureUnit);

    /// <summary>Validator — see <see cref="UpdateMyTimeFormat.Validator"/> for the rationale.</summary>
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

            var tracked = await _db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
            if (tracked is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "User not found.");
            }

            tracked.TemperatureUnit = request.TemperatureUnit;
            await _db.SaveChangesAsync(cancellationToken);

            return new Response(request.TemperatureUnit);
        }
    }
}
