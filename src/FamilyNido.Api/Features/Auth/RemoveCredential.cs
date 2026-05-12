using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Slice: an authenticated user deletes one of their own credentials.
/// Refuses to remove the last credential to prevent self-lockout.
/// </summary>
public static class RemoveCredential
{
    /// <summary>Command.</summary>
    /// <param name="CredentialId">Credential to remove.</param>
    public sealed record Command(Guid CredentialId) : IRequest<Result<Unit>>;

    /// <summary>Handler.</summary>
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
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken ct)
        {
            var user = await _userContext.GetUserAsync(ct);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Caller is not authenticated.");
            }

            var credential = await _db.UserCredentials
                .FirstOrDefaultAsync(c => c.Id == request.CredentialId && c.UserId == user.Id, ct);

            if (credential is null)
            {
                return ApplicationError.NotFound("credential.not_found", "Credential not found.");
            }

            var totalForUser = await _db.UserCredentials.CountAsync(c => c.UserId == user.Id, ct);
            if (totalForUser <= 1)
            {
                return ApplicationError.Conflict(
                    "credential.last_remaining",
                    "Cannot remove the only login method on this account. Add another method first.");
            }

            _db.UserCredentials.Remove(credential);
            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
