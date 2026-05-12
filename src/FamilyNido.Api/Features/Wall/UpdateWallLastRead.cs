using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Wall;

/// <summary>
/// Slice: bump the caller's <c>LastWallReadAt</c> watermark to <c>now</c>, used to
/// reset the unread counter when the UI opens the wall (RF-WALL-010). Idempotent.
/// </summary>
public static class UpdateWallLastRead
{
    /// <summary>Parameterless command — all inputs come from the authenticated user.</summary>
    public sealed record Command : IRequest<Result<Unit>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _clock;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext, TimeProvider clock)
        {
            _db = db;
            _userContext = userContext;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == current.User.Id, cancellationToken);
            if (user is null)
            {
                return ApplicationError.NotFound("user.not_found", "Authenticated user row missing.");
            }

            user.LastWallReadAt = _clock.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
