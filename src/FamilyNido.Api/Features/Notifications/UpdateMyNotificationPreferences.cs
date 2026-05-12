using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Notifications;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Slice for <c>PUT /api/notifications/preferences</c>. Upserts the caller's
/// preferences row — creates it on first save, then updates in place.
/// </summary>
public static class UpdateMyNotificationPreferences
{
    /// <summary>Command carries the full set of toggles (replace, not patch).</summary>
    public sealed record Command(
        bool EmailEnabled,
        bool DigestEnabled,
        bool TaskAssignedEnabled,
        bool WallMentionEnabled) : IRequest<Result<NotificationPreferencesDto>>;

    /// <summary>Performs the upsert.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<NotificationPreferencesDto>>
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
        public async Task<Result<NotificationPreferencesDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var user = await _userContext.GetUserAsync(cancellationToken);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Not signed in.");
            }

            var prefs = await _db.UserNotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

            if (prefs is null)
            {
                prefs = new UserNotificationPreferences { UserId = user.Id };
                _db.UserNotificationPreferences.Add(prefs);
            }

            prefs.EmailEnabled = request.EmailEnabled;
            prefs.DigestEnabled = request.DigestEnabled;
            prefs.TaskAssignedEnabled = request.TaskAssignedEnabled;
            prefs.WallMentionEnabled = request.WallMentionEnabled;

            await _db.SaveChangesAsync(cancellationToken);

            return new NotificationPreferencesDto(
                prefs.EmailEnabled,
                prefs.DigestEnabled,
                prefs.TaskAssignedEnabled,
                prefs.WallMentionEnabled);
        }
    }
}
