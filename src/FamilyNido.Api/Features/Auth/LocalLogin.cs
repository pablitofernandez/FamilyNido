using System.Security.Claims;
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

namespace FamilyNido.Api.Features.Auth;

/// <summary>
/// Slice: anonymous local-credentials login. Returns a generic 401 for both
/// "user does not exist" and "password mismatch" so the endpoint cannot be
/// used to enumerate accounts. The endpoint signs the cookie itself instead
/// of going through ASP.NET's challenge dance, so the cookie ends up
/// equivalent in shape to one minted by the OIDC callback (same claim set).
/// </summary>
public static class LocalLogin
{
    /// <summary>Command — wire payload from the front.</summary>
    /// <param name="Email">Account email (case-insensitive).</param>
    /// <param name="Password">Plain-text password.</param>
    public sealed record Command(string Email, string Password) : IRequest<Result<LocalLoginResponse>>;

    /// <summary>Input validation. Mirrors what the front already checks; backend stays the source of truth.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
            RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<LocalLoginResponse>>
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
        public async Task<Result<LocalLoginResponse>> HandleAsync(Command request, CancellationToken ct)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            // Pull the user with its local credential. EmailIndex is unique
            // so this is a single index lookup. We materialize the row even
            // for non-existing users to keep the timing similar across
            // outcomes — minor mitigation against trivial timing oracles.
            var user = await _db.Users
                .Include(u => u.Credentials)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

            var credential = user?.Credentials.FirstOrDefault(c => c.Provider == IdentityProvider.Local);

            if (user is null || credential is null || string.IsNullOrEmpty(credential.PasswordHash))
            {
                // Hash the supplied password against a dummy hash so the
                // failure path takes a similar amount of time as success.
                _passwordHasher.VerifyHashedPassword(new User { Email = "x", DisplayName = "x" }, DummyHash, request.Password);
                return ApplicationError.Forbidden("auth.invalid_credentials", "Invalid credentials.");
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return ApplicationError.Forbidden("auth.invalid_credentials", "Invalid credentials.");
            }

            // Quietly upgrade the hash format if the framework recommends it
            // (e.g. higher iteration count after a SDK upgrade).
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                credential.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            }

            var now = _timeProvider.GetUtcNow();
            user.LastLoginAt = now;
            credential.LastUsedAt = now;
            await _db.SaveChangesAsync(ct);

            // Sign the user in directly with the cookie scheme — this is
            // shaped exactly like the cookie minted by the OIDC callback so
            // CurrentUserContext, policies and audit can resolve it the
            // same way (via the userId claim).
            var http = _httpContextAccessor.HttpContext
                       ?? throw new InvalidOperationException("LocalLogin requires an HTTP context.");
            await SignInAsync(http, user);

            return new LocalLoginResponse(user.Id);
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

        // Pre-computed PBKDF2 IdentityV3 hash for the constant string "dummy".
        // Used solely to keep timing similar between "no such user" and
        // "wrong password" branches; never accepted by production code paths.
        private const string DummyHash = "AQAAAAIAAYagAAAAEN7N9wzjC0Q5xLsv6T6bC2jD7M5KZ8Z7VlFpYy5UQS3SkQ8Pxn6M8Tcu1gZ6lGqcYg==";
    }
}

/// <summary>Result of <c>POST /api/auth/local/login</c>.</summary>
/// <param name="UserId">Internal user id (front uses this only as a sanity flag).</param>
public sealed record LocalLoginResponse(Guid UserId);
