using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>Slice for <c>PUT /api/school/extracurriculars/{id}</c>.</summary>
public static class UpdateExtracurricular
{
    /// <summary>Command replacing the editable fields of an activity row.</summary>
    public sealed record Command(
        Guid Id,
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

    /// <summary>Input validation — mirrors <see cref="AddExtracurricular.Validator"/>.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Location).MaximumLength(160);
            RuleFor(x => x.ContactPhone).MaximumLength(40);
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleFor(x => x.WeeklyDays).NotEqual(DayOfWeekMask.None);
            RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime);
            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.EndDate is not null);
        }
    }

    /// <summary>Updates after verifying family scope.</summary>
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

            var entry = await _db.Extracurriculars.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
            if (entry is null || entry.FamilyId != current.Family.Id)
            {
                return ApplicationError.NotFound("school.extracurricular.not_found", $"Activity {request.Id} not found.");
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

            entry.FamilyMemberId = request.MemberId;
            entry.Name = request.Name.Trim();
            entry.Location = Trim(request.Location);
            entry.ContactPhone = Trim(request.ContactPhone);
            entry.WeeklyDays = request.WeeklyDays;
            entry.StartTime = request.StartTime;
            entry.EndTime = request.EndTime;
            entry.StartDate = request.StartDate;
            entry.EndDate = request.EndDate;
            entry.DefaultDropoffMemberId = request.DefaultDropoffMemberId;
            entry.DefaultPickupMemberId = request.DefaultPickupMemberId;
            entry.Notes = Trim(request.Notes);

            await _db.SaveChangesAsync(cancellationToken);
            return AddExtracurricular.Handler.ToDto(entry);
        }

        private static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
