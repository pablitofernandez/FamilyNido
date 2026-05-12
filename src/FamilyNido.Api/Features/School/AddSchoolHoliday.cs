using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.School;
using FamilyNido.Persistence;
using FluentValidation;

namespace FamilyNido.Api.Features.School;

/// <summary>Slice for <c>POST /api/school/holidays</c>.</summary>
public static class AddSchoolHoliday
{
    /// <summary>Command for a new holiday range.</summary>
    public sealed record Command(DateOnly StartDate, DateOnly EndDate, string Label) : IRequest<Result<SchoolHolidayDto>>;

    /// <summary>Validation: start &lt;= end and a label of reasonable length.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
            RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        }
    }

    /// <summary>Persists the holiday inside the caller's family.</summary>
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

            var entry = new SchoolHoliday
            {
                FamilyId = current.Family.Id,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Label = request.Label.Trim(),
            };
            _db.SchoolHolidays.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);

            return new SchoolHolidayDto(entry.Id, entry.StartDate, entry.EndDate, entry.Label);
        }
    }
}
