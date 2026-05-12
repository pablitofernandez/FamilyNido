using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Health;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Health;

/// <summary>Slice for <c>POST /api/health/members/{memberId}/vaccinations</c>.</summary>
public static class AddVaccination
{
    /// <summary>Command for a new vaccination row.</summary>
    public sealed record Command(
        Guid MemberId,
        string Name,
        DateOnly Date,
        DateOnly? NextDueDate,
        string? Notes) : IRequest<Result<VaccinationDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleFor(x => x.NextDueDate)
                .GreaterThanOrEqualTo(x => x.Date)
                .When(x => x.NextDueDate is not null)
                .WithMessage("La próxima dosis debe ser posterior o igual a la fecha actual.");
        }
    }

    /// <summary>Inserts after verifying family scope.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<VaccinationDto>>
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
        public async Task<Result<VaccinationDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var memberOk = await _db.FamilyMembers
                .AnyAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, cancellationToken);
            if (!memberOk)
            {
                return ApplicationError.NotFound("family_member.not_found", $"Member {request.MemberId} not found.");
            }

            var entry = new Vaccination
            {
                FamilyMemberId = request.MemberId,
                Name = request.Name.Trim(),
                Date = request.Date,
                NextDueDate = request.NextDueDate,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            };

            _db.Vaccinations.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);

            return new VaccinationDto(entry.Id, entry.Name, entry.Date, entry.NextDueDate, entry.Notes);
        }
    }
}
