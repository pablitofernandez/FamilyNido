using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>Slice: trigger an inline sync of a single Google account on demand (RF-CAL-005).</summary>
public static class TriggerManualSync
{
    /// <summary>Command — identifies the account to sync.</summary>
    /// <param name="GoogleAccountId">Id of the account to sync.</param>
    public sealed record Command(Guid GoogleAccountId) : IRequest<Result<GoogleAccountDto>>;

    /// <summary>Handler — runs the synchronizer, then returns the updated DTO.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<GoogleAccountDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly CalendarSynchronizer _synchronizer;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            CalendarSynchronizer synchronizer)
        {
            _db = db;
            _userContext = userContext;
            _synchronizer = synchronizer;
        }

        /// <inheritdoc />
        public async Task<Result<GoogleAccountDto>> HandleAsync(Command request, CancellationToken cancellationToken)
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

            await _synchronizer.SyncAccountAsync(account.Id, cancellationToken);

            // Re-load with calendars attached to surface fresh LastSyncedAt values.
            var refreshed = await _db.GoogleAccounts
                .AsNoTracking()
                .Include(a => a.Calendars)
                .FirstAsync(a => a.Id == account.Id, cancellationToken);

            return GoogleAccountDto.From(refreshed);
        }
    }
}
