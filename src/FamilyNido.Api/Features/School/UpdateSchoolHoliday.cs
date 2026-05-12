using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>Slice for <c>PUT /api/school/holidays/{id}</c>.</summary>
public static class UpdateSchoolHoliday
{
    /// <summary>Command replacing the editable fields of a holiday row.</summary>
    public sealed record Command(Guid Id, DateOnly StartDate, DateOnly EndDate, string Label)
        : IRequest<Result<SchoolHolidayDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
            RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        }
    }

    /// <summary>Updates after verifying family scope.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<SchoolHolidayDto>>
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
        public async Task<Result<SchoolHolidayDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var entry = await _db.SchoolHolidays.FirstOrDefaultAsync(h => h.Id == request.Id, cancellationToken);
            if (entry is null || entry.FamilyId != current.Family.Id)
            {
                return ApplicationError.NotFound("school.holiday.not_found", $"Holiday {request.Id} not found.");
            }

            entry.StartDate = request.StartDate;
            entry.EndDate = request.EndDate;
            entry.Label = request.Label.Trim();

            await _db.SaveChangesAsync(cancellationToken);

            return new SchoolHolidayDto(entry.Id, entry.StartDate, entry.EndDate, entry.Label);
        }
    }
}
