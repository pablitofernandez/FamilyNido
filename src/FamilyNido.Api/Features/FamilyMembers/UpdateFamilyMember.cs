using System.Text.RegularExpressions;
using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>Slice: update mutable fields of an existing member (RF-USR-003).</summary>
public static partial class UpdateFamilyMember
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();

    /// <summary>Command carrying the target id and the new values.</summary>
    /// <param name="MemberId">Target member id.</param>
    /// <param name="DisplayName">Name shown in the UI. 1-120 chars.</param>
    /// <param name="ColorHex">Hex color (#RRGGBB).</param>
    /// <param name="BirthDate">Optional date of birth.</param>
    /// <param name="ContactEmail">Optional informational contact email.</param>
    public sealed record Command(
        Guid MemberId,
        string DisplayName,
        string ColorHex,
        DateOnly? BirthDate,
        string? ContactEmail) : IRequest<Result<FamilyMemberDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.DisplayName)
                .NotEmpty()
                .MaximumLength(120);

            RuleFor(x => x.ColorHex)
                .NotEmpty()
                .Matches(HexColorRegex())
                .WithMessage("ColorHex must be in #RRGGBB format.");

            RuleFor(x => x.ContactEmail)
                .EmailAddress()
                .MaximumLength(254)
                .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

            RuleFor(x => x.BirthDate)
                .Must(d => d is null || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("BirthDate cannot be in the future.");
        }
    }

    /// <summary>Handler — writes the mutable fields to the tracked entity.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<FamilyMemberDto>>
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
        public async Task<Result<FamilyMemberDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var member = await _db.FamilyMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(
                    m => m.Id == request.MemberId && m.FamilyId == current.Family.Id,
                    cancellationToken);

            if (member is null)
            {
                return ApplicationError.NotFound(
                    "family_member.not_found",
                    $"Member {request.MemberId} not found in family.");
            }

            member.DisplayName = request.DisplayName;
            member.ColorHex = request.ColorHex;
            member.BirthDate = request.BirthDate;
            member.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail;

            await _db.SaveChangesAsync(cancellationToken);

            return FamilyMemberDto.From(member);
        }
    }
}
