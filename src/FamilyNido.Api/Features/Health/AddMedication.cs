using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Health;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Health;

/// <summary>Slice for <c>POST /api/health/members/{memberId}/medications</c>.</summary>
public static class AddMedication
{
    /// <summary>Command for a new medication row.</summary>
    public sealed record Command(
        Guid MemberId,
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
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Dose).MaximumLength(80);
            RuleFor(x => x.Frequency).MaximumLength(120);
            RuleFor(x => x.Instructions).MaximumLength(2000);
            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.EndDate is not null)
                .WithMessage("La fecha de fin debe ser posterior o igual a la de inicio.");
        }
    }

    /// <summary>Inserts after verifying family scope.</summary>
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

            var memberOk = await _db.FamilyMembers
                .AnyAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, cancellationToken);
            if (!memberOk)
            {
                return ApplicationError.NotFound("family_member.not_found", $"Member {request.MemberId} not found.");
            }

            var entry = new Medication
            {
                FamilyMemberId = request.MemberId,
                Name = request.Name.Trim(),
                Dose = Trim(request.Dose),
                Frequency = Trim(request.Frequency),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Instructions = Trim(request.Instructions),
            };

            _db.Medications.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var isActive = entry.EndDate is null || entry.EndDate >= today;
            return new MedicationDto(entry.Id, entry.Name, entry.Dose, entry.Frequency, entry.StartDate, entry.EndDate, entry.Instructions, isActive);
        }

        private static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
