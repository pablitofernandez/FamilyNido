using System.Text.RegularExpressions;
using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>Slice: create a new <see cref="FamilyMember"/> under the caller's family (RF-USR-002).</summary>
public static partial class CreateFamilyMember
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();

    /// <summary>Command carrying the input payload.</summary>
    /// <param name="DisplayName">Name shown in the UI. 1-120 chars.</param>
    /// <param name="MemberType">Kind of member.</param>
    /// <param name="ColorHex">Hex color (#RRGGBB) to use consistently across the app.</param>
    /// <param name="BirthDate">Optional date of birth.</param>
    /// <param name="ContactEmail">Optional informational contact email.</param>
    public sealed record Command(
        string DisplayName,
        MemberType MemberType,
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

    /// <summary>Handler — persists a new family member row.</summary>
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

            var familyExists = await _db.Families.AnyAsync(f => f.Id == current.Family.Id, cancellationToken);
            if (!familyExists)
            {
                return ApplicationError.NotFound("family.not_found", $"Family {current.Family.Id} does not exist.");
            }

            var member = new FamilyMember
            {
                FamilyId = current.Family.Id,
                DisplayName = request.DisplayName,
                MemberType = request.MemberType,
                ColorHex = request.ColorHex,
                BirthDate = request.BirthDate,
                ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail,
            };

            _db.FamilyMembers.Add(member);
            await _db.SaveChangesAsync(cancellationToken);

            return FamilyMemberDto.From(member);
        }
    }
}
