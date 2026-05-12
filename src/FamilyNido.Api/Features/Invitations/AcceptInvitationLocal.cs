using System.Security.Claims;
using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Slice: an anonymous caller accepts an invitation by setting a local
/// password (no OIDC required). Atomically: validates the token, creates a
/// <see cref="User"/> + a Local <see cref="UserCredential"/>, links the
/// target <see cref="Domain.Families.FamilyMember"/>, signs the cookie, and
/// returns the new user id.
/// </summary>
public static class AcceptInvitationLocal
{
    /// <summary>Command.</summary>
    /// <param name="Token">Raw invitation token from the URL.</param>
    /// <param name="Password">Plain-text password chosen by the recipient.</param>
    public sealed record Command(string Token, string Password) : IRequest<Result<AcceptInvitationResponse>>;

    /// <summary>Validator. Reuses the central password policy.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.Password).Password();
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<AcceptInvitationResponse>>
    {
        private readonly ApplicationDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TimeProvider _timeProvider;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            IPasswordHasher<User> passwordHasher,
            IHttpContextAccessor httpContextAccessor,
            TimeProvider timeProvider)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _httpContextAccessor = httpContextAccessor;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<Result<AcceptInvitationResponse>> HandleAsync(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return ApplicationError.NotFound("invitation.not_found", "Invitation not found.");
            }

            var hash = InvitationToken.Hash(request.Token);
            var invitation = await _db.Invitations
                .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);

            if (invitation is null)
            {
                return ApplicationError.NotFound("invitation.not_found", "Invitation not found.");
            }

            // The handler is "anonymous" so it must own the whole transaction
            // explicitly: token consumption, user creation, member linking
            // and cookie sign-in either all succeed or none does. Postgres
            // gives us serializable-by-default semantics here through EF.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var now = _timeProvider.GetUtcNow();
            var consumedRows = await _db.Invitations
                .Where(i => i.Id == invitation.Id
                    && i.ConsumedAt == null
                    && i.RevokedAt == null
                    && i.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.ConsumedAt, now), ct);

            if (consumedRows == 0)
            {
                return ApplicationError.Conflict(
                    "invitation.unavailable",
                    "This invitation has already been used, expired, or was revoked.");
            }

            // If a user with that email already exists, refuse: this path is
            // for *new* accounts. Existing users must accept-oidc (after OIDC
            // login) or set a password from "Mi cuenta".
            var emailExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == invitation.Email, ct);
            if (emailExists)
            {
                return ApplicationError.Conflict(
                    "user.email_already_registered",
                    "An account with this email already exists. Use the matching login method or set a password from your account screen.");
            }

            var member = await _db.FamilyMembers.FirstOrDefaultAsync(m => m.Id == invitation.FamilyMemberId, ct);
            if (member is null || member.UserId is not null)
            {
                return ApplicationError.Conflict(
                    "family_member.already_linked",
                    "Target member is already linked to another user.");
            }

            // Bootstrap the new identity: User with the role configured at
            // invitation time + a single Local credential.
            var user = new User
            {
                Email = invitation.Email,
                DisplayName = member.DisplayName,
                Role = invitation.RoleOnAccept,
                LastLoginAt = now,
            };
            _db.Users.Add(user);

            var hashed = _passwordHasher.HashPassword(user, request.Password);
            _db.UserCredentials.Add(new UserCredential
            {
                UserId = user.Id,
                User = user,
                Provider = IdentityProvider.Local,
                PasswordHash = hashed,
                LastUsedAt = now,
            });

            // Bind the member to the brand-new user and stamp the
            // ConsumedByUserId now that we know the id.
            member.UserId = user.Id;
            await _db.Invitations
                .Where(i => i.Id == invitation.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.ConsumedByUserId, (Guid?)user.Id), ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Sign the cookie now so the caller is logged in upon return.
            var http = _httpContextAccessor.HttpContext
                       ?? throw new InvalidOperationException("AcceptInvitationLocal requires an HTTP context.");
            await SignInAsync(http, user);

            return new AcceptInvitationResponse(
                FamilyMemberId: invitation.FamilyMemberId,
                FamilyId: invitation.FamilyId,
                Role: invitation.RoleOnAccept);
        }

        private static async Task SignInAsync(HttpContext http, User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(CurrentUserContext.UserIdClaimType, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Role, user.Role.ToString()),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
    }
}
