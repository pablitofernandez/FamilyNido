using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Slice: an authenticated user sets or rotates their local password.
/// First-time setup (no existing local credential) does not require the
/// current password; rotation does.
/// </summary>
public static class SetLocalPassword
{
    /// <summary>Command.</summary>
    /// <param name="CurrentPassword">Current password (required when rotating; ignored on first-time setup).</param>
    /// <param name="NewPassword">New password.</param>
    public sealed record Command(string? CurrentPassword, string NewPassword) : IRequest<Result<Unit>>;

    /// <summary>Validator — only checks the new password's complexity here. Current-password verification happens in the handler against the stored hash.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.NewPassword).Password();
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider _timeProvider;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            IPasswordHasher<User> passwordHasher,
            TimeProvider timeProvider)
        {
            _db = db;
            _userContext = userContext;
            _passwordHasher = passwordHasher;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken ct)
        {
            var user = await _userContext.GetUserAsync(ct);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Caller is not authenticated.");
            }

            var existing = await _db.UserCredentials
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Provider == IdentityProvider.Local, ct);

            if (existing is not null)
            {
                if (string.IsNullOrEmpty(request.CurrentPassword))
                {
                    return ApplicationError.Validation(
                        "auth.current_password_required",
                        "Current password is required to rotate the local credential.");
                }

                var verification = _passwordHasher.VerifyHashedPassword(
                    user, existing.PasswordHash!, request.CurrentPassword);
                if (verification == PasswordVerificationResult.Failed)
                {
                    return ApplicationError.Forbidden("auth.invalid_credentials", "Current password is incorrect.");
                }

                existing.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
                existing.UpdatedAt = _timeProvider.GetUtcNow();
            }
            else
            {
                _db.UserCredentials.Add(new UserCredential
                {
                    UserId = user.Id,
                    Provider = IdentityProvider.Local,
                    PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword),
                });
            }

            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }
}
