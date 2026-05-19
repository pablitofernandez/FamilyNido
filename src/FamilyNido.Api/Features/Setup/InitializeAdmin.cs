using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Setup;

/// <summary>
/// Slice for the anonymous one-shot <c>POST /api/setup/initial-admin</c>.
/// Bootstraps a brand new instance: creates the <see cref="Family"/>, the
/// first <see cref="User"/> with role <c>Admin</c>, the linked
/// <see cref="FamilyMember"/> and the local <see cref="UserCredential"/> for
/// the password — all in one transaction. Refuses with <c>409 Conflict</c>
/// the moment any user already exists, so a curious visitor can't hijack
/// an instance that's already been claimed.
/// </summary>
public static class InitializeAdmin
{
    /// <summary>Family section of the command body.</summary>
    /// <param name="Name">Display name shown in the shell header.</param>
    /// <param name="TimeZone">IANA timezone id (e.g. <c>America/New_York</c>).</param>
    public sealed record FamilyInput(string Name, string TimeZone);

    /// <summary>Admin section of the command body.</summary>
    /// <param name="Email">Login email.</param>
    /// <param name="DisplayName">Name shown in the UI for the linked member.</param>
    /// <param name="Password">Plain-text password — hashed before persisting.</param>
    public sealed record AdminInput(string Email, string DisplayName, string Password);

    /// <summary>Composite command — both blocks always supplied together.</summary>
    public sealed record Command(FamilyInput Family, AdminInput Admin) : IRequest<Result<Unit>>;

    /// <summary>
    /// Validator. Mirrors the local-credentials password policy via
    /// <see cref="PasswordPolicy.Password{T}"/> so the strength bar in the
    /// wizard matches the one users hit later on <c>/account</c>.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Builds the rules.</summary>
        public Validator()
        {
            RuleFor(x => x.Family.Name)
                .NotEmpty()
                .MaximumLength(120);

            RuleFor(x => x.Family.TimeZone)
                .NotEmpty()
                .MaximumLength(64)
                .Must(BeKnownTimeZone)
                .WithMessage("Unknown IANA time zone.");

            RuleFor(x => x.Admin.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(254);

            RuleFor(x => x.Admin.DisplayName)
                .NotEmpty()
                .MaximumLength(120);

            RuleFor(x => x.Admin.Password).Password();
        }

        private static bool BeKnownTimeZone(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
    {
        private readonly ApplicationDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        /// <inheritdoc />
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            // Guard against re-initialising an instance that's already been
            // claimed. The check + insert race is closed by the unique index
            // on Users.Email — a duplicate POST that races past this check
            // gets a DbUpdateException which we don't try to handle (500 is
            // the right answer for a 1-in-a-million bootstrap dogpile).
            if (await _db.Users.AnyAsync(cancellationToken))
            {
                return ApplicationError.Conflict(
                    "setup.already_initialized",
                    "The instance has already been initialised.");
            }

            var family = new Family
            {
                Name = request.Family.Name.Trim(),
                TimeZone = request.Family.TimeZone,
                // Default the UI bundle to en-US — the user can flip it from
                // /account → "Idioma de la interfaz" once they're in.
                Locale = "en-US",
            };
            _db.Families.Add(family);

            var user = new User
            {
                Email = request.Admin.Email.Trim().ToLowerInvariant(),
                DisplayName = request.Admin.DisplayName.Trim(),
                Role = FamilyRole.Admin,
                PreferredLanguage = "en-US",
            };
            _db.Users.Add(user);

            var member = new FamilyMember
            {
                FamilyId = family.Id,
                DisplayName = request.Admin.DisplayName.Trim(),
                MemberType = MemberType.Adult,
                // A neutral default colour that visually matches the brand;
                // the admin can pick something else on /nido later.
                ColorHex = "#C96442",
                UserId = user.Id,
            };
            _db.FamilyMembers.Add(member);

            _db.UserCredentials.Add(new UserCredential
            {
                UserId = user.Id,
                Provider = IdentityProvider.Local,
                PasswordHash = _passwordHasher.HashPassword(user, request.Admin.Password),
            });

            await _db.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
