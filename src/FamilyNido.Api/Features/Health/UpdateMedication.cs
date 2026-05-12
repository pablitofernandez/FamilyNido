using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Health;

/// <summary>Slice for <c>PUT /api/health/medications/{id}</c>.</summary>
public static class UpdateMedication
{
    /// <summary>Command replacing the editable fields of a medication row.</summary>
    public sealed record Command(
        Guid Id,
        string Name,
        string? Dose,
        string? Frequency,
        DateOnly StartDate,
        DateOnly? EndDate,
        string? Instructions) : IRequest<Result<MedicationDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Dose).MaximumLength(80);
            RuleFor(x => x.Frequency).MaximumLength(120);
            RuleFor(x => x.Instructions).MaximumLength(2000);
            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.EndDate is not null);
        }
    }

    /// <summary>Updates after verifying family scope.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<MedicationDto>>
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
        public async Task<Result<MedicationDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var entry = await _db.Medications
                .Include(m => m.FamilyMember)
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
            if (entry is null || entry.FamilyMember!.FamilyId != current.Family.Id)
            {
                return ApplicationError.NotFound("medication.not_found", $"Medication {request.Id} not found.");
            }

            entry.Name = request.Name.Trim();
            entry.Dose = Trim(request.Dose);
            entry.Frequency = Trim(request.Frequency);
            entry.StartDate = request.StartDate;
            entry.EndDate = request.EndDate;
            entry.Instructions = Trim(request.Instructions);

            await _db.SaveChangesAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var isActive = entry.EndDate is null || entry.EndDate >= today;
            return new MedicationDto(entry.Id, entry.Name, entry.Dose, entry.Frequency, entry.StartDate, entry.EndDate, entry.Instructions, isActive);
        }

        private static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
