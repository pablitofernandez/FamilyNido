using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Domain.School;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>Slice for <c>POST /api/school/extracurriculars</c>.</summary>
public static class AddExtracurricular
{
    /// <summary>Command for a new extracurricular row.</summary>
    public sealed record Command(
        Guid MemberId,
        string Name,
        string? Location,
        string? ContactPhone,
        DayOfWeekMask WeeklyDays,
        TimeOnly StartTime,
        TimeOnly EndTime,
        DateOnly StartDate,
        DateOnly? EndDate,
        Guid? DefaultDropoffMemberId,
        Guid? DefaultPickupMemberId,
        string? Notes) : IRequest<Result<ExtracurricularDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Location).MaximumLength(160);
            RuleFor(x => x.ContactPhone).MaximumLength(40);
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleFor(x => x.WeeklyDays).NotEqual(DayOfWeekMask.None)
                .WithMessage("Pick at least one weekday.");
            RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime)
                .WithMessage("End time must be after start time.");
            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.EndDate is not null);
        }
    }

    /// <summary>Persists the row after validating member references.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<ExtracurricularDto>>
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
        public async Task<Result<ExtracurricularDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var ids = new HashSet<Guid> { request.MemberId };
            if (request.DefaultDropoffMemberId is { } d) ids.Add(d);
            if (request.DefaultPickupMemberId is { } p) ids.Add(p);

            var found = await _db.FamilyMembers
                .Where(m => ids.Contains(m.Id) && m.FamilyId == current.Family.Id)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            if (found.Count != ids.Count)
            {
                return ApplicationError.Validation(
                    "school.extracurricular.unknown_member",
                    "One or more referenced members are not part of this family.");
            }

            var entry = new Extracurricular
            {
                FamilyId = current.Family.Id,
                FamilyMemberId = request.MemberId,
                Name = request.Name.Trim(),
                Location = Trim(request.Location),
                ContactPhone = Trim(request.ContactPhone),
                WeeklyDays = request.WeeklyDays,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                DefaultDropoffMemberId = request.DefaultDropoffMemberId,
                DefaultPickupMemberId = request.DefaultPickupMemberId,
                Notes = Trim(request.Notes),
            };

            _db.Extracurriculars.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);

            return ToDto(entry);
        }

        private static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        internal static ExtracurricularDto ToDto(Extracurricular e) => new(
            e.Id, e.FamilyMemberId, e.Name, e.Location, e.ContactPhone,
            e.WeeklyDays, e.StartTime, e.EndTime, e.StartDate, e.EndDate,
            e.DefaultDropoffMemberId, e.DefaultPickupMemberId, e.Notes, e.IsArchived);
    }
}
