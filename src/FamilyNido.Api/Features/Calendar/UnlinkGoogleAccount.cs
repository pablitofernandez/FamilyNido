using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Slice: drop a linked Google account along with its calendars and cached events.
/// The owning user (or an admin) is the only one allowed to unlink — otherwise any
/// adult could wipe another user's integration.
/// </summary>
public static class UnlinkGoogleAccount
{
    /// <summary>Command — identifies the account to unlink.</summary>
    /// <param name="GoogleAccountId">Id of the account row.</param>
    public sealed record Command(Guid GoogleAccountId) : IRequest<Result<Unit>>;

    /// <summary>Carries no value; signals "completed" to the endpoint.</summary>
    public sealed record Unit;

    /// <summary>Handler — deletes the row; cascade removes calendars and events.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
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
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var account = await _db.GoogleAccounts
                .FirstOrDefaultAsync(
                    a => a.Id == request.GoogleAccountId && a.FamilyId == current.Family.Id,
                    cancellationToken);

            if (account is null)
            {
                return ApplicationError.NotFound(
                    "calendar.account_not_found",
                    "La cuenta vinculada no existe o no pertenece a tu familia.");
            }

            var isOwner = account.UserId == current.User.Id;
            var isAdmin = current.User.Role == Domain.Families.FamilyRole.Admin;
            if (!isOwner && !isAdmin)
            {
                return ApplicationError.Forbidden(
                    "calendar.account_forbidden",
                    "Solo el dueño de la cuenta o un admin puede desvincularla.");
            }

            _db.GoogleAccounts.Remove(account);
            await _db.SaveChangesAsync(cancellationToken);
            return new Unit();
        }
    }
}
