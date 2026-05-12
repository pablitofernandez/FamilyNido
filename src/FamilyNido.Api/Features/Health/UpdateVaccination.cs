using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Health;

/// <summary>Slice for <c>PUT /api/health/vaccinations/{id}</c>.</summary>
public static class UpdateVaccination
{
    /// <summary>Command replacing the editable fields of a vaccination row.</summary>
    public sealed record Command(
        Guid Id,
        string Name,
        DateOnly Date,
        DateOnly? NextDueDate,
        string? Notes) : IRequest<Result<VaccinationDto>>;

    /// <summary>Input validation — mirrors <see cref="AddVaccination.Validator"/>.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleFor(x => x.NextDueDate)
                .GreaterThanOrEqualTo(x => x.Date)
                .When(x => x.NextDueDate is not null);
        }
    }

    /// <summary>Updates after verifying family scope.</summary>
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

            var entry = await _db.Vaccinations
                .Include(v => v.FamilyMember)
                .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
            if (entry is null || entry.FamilyMember!.FamilyId != current.Family.Id)
            {
                return ApplicationError.NotFound("vaccination.not_found", $"Vaccination {request.Id} not found.");
            }

            entry.Name = request.Name.Trim();
            entry.Date = request.Date;
            entry.NextDueDate = request.NextDueDate;
            entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            await _db.SaveChangesAsync(cancellationToken);

            return new VaccinationDto(entry.Id, entry.Name, entry.Date, entry.NextDueDate, entry.Notes);
        }
    }
}
