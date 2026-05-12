using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.School;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>PUT /api/school/day-schedule/exceptions/{memberId}/{date}</c>.
/// Upserts a per-date override of the school commute. Either flag the day as
/// cancelled or reassign drop-off / pick-up — both fields can be set
/// simultaneously when both legs of the day change.
/// </summary>
public static class SetSchoolDayException
{
    /// <summary>Command carrying the (kid, date) tuple and the new state.</summary>
    public sealed record Command(
        Guid MemberId,
        DateOnly Date,
        bool IsCancelled,
        Guid? DropoffMemberId,
        Guid? PickupMemberId,
        TimeOnly? MorningTime,
        TimeOnly? AfternoonTime,
        string? Notes) : IRequest<Result<SchoolDayExceptionDto>>;

    /// <summary>Validation: must either cancel or set at least one caretaker change.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Notes).MaximumLength(500);
            RuleFor(x => x)
                .Must(c => c.IsCancelled
                    || c.DropoffMemberId is not null
                    || c.PickupMemberId is not null
                    || c.MorningTime is not null
                    || c.AfternoonTime is not null
                    || !string.IsNullOrWhiteSpace(c.Notes))
                .WithMessage("Provide at least one change (cancel, override drop-off / pick-up, custom times, or a note).");
        }
    }

    /// <summary>Inserts or updates the row.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<SchoolDayExceptionDto>>
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
        public async Task<Result<SchoolDayExceptionDto>> HandleAsync(Command request, CancellationToken cancellationToken)
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

            // Caretaker references must belong to the same family.
            var caretakerIds = new List<Guid>();
            if (request.DropoffMemberId is { } d) caretakerIds.Add(d);
            if (request.PickupMemberId is { } p) caretakerIds.Add(p);
            if (caretakerIds.Count > 0)
            {
                var found = await _db.FamilyMembers
                    .CountAsync(m => caretakerIds.Contains(m.Id) && m.FamilyId == current.Family.Id, cancellationToken);
                if (found != caretakerIds.Distinct().Count())
                {
                    return ApplicationError.Validation(
                        "school.day_schedule.unknown_caretaker",
                        "Caretaker is not part of this family.");
                }
            }

            var entry = await _db.SchoolDayExceptions
                .FirstOrDefaultAsync(e => e.FamilyMemberId == request.MemberId && e.Date == request.Date, cancellationToken);

            if (entry is null)
            {
                entry = new SchoolDayException
                {
                    FamilyId = current.Family.Id,
                    FamilyMemberId = request.MemberId,
                    Date = request.Date,
                };
                _db.SchoolDayExceptions.Add(entry);
            }

            entry.IsCancelled = request.IsCancelled;
            entry.DropoffMemberId = request.IsCancelled ? null : request.DropoffMemberId;
            entry.PickupMemberId = request.IsCancelled ? null : request.PickupMemberId;
            entry.MorningTime = request.IsCancelled ? null : request.MorningTime;
            entry.AfternoonTime = request.IsCancelled ? null : request.AfternoonTime;
            entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            await _db.SaveChangesAsync(cancellationToken);

            return new SchoolDayExceptionDto(
                entry.Id,
                entry.FamilyMemberId,
                entry.Date,
                entry.IsCancelled,
                entry.DropoffMemberId,
                entry.PickupMemberId,
                entry.MorningTime,
                entry.AfternoonTime,
                entry.Notes);
        }
    }
}
