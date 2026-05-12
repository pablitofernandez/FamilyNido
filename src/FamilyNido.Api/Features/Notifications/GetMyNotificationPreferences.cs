using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Notifications;

/// <summary>
/// Slice for <c>GET /api/notifications/preferences</c>. Returns the caller's
/// preferences row or, when missing, the implicit "everything on" defaults.
/// </summary>
public static class GetMyNotificationPreferences
{
    /// <summary>Query carries no payload — the caller is the implicit subject.</summary>
    public sealed record Query : IRequest<Result<NotificationPreferencesDto>>;

    /// <summary>Reads <c>UserNotificationPreferences</c> for the caller.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<NotificationPreferencesDto>>
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
        public async Task<Result<NotificationPreferencesDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var user = await _userContext.GetUserAsync(cancellationToken);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Not signed in.");
            }

            var prefs = await _db.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

            // No row yet → treat as defaults; the row is created on first PUT.
            return new NotificationPreferencesDto(
                EmailEnabled: prefs?.EmailEnabled ?? true,
                DigestEnabled: prefs?.DigestEnabled ?? true,
                TaskAssignedEnabled: prefs?.TaskAssignedEnabled ?? true,
                WallMentionEnabled: prefs?.WallMentionEnabled ?? true);
        }
    }
}
