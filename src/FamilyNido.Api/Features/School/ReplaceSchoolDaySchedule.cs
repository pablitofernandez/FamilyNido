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
/// Slice for <c>PUT /api/school/members/{memberId}/day-schedule</c>. Replaces
/// the entire weekly pattern of one kid in a single shot. Sending an empty
/// list clears the schedule; sending rows with both caretakers null is
/// rejected — at least one slot must be set.
/// </summary>
public static class ReplaceSchoolDaySchedule
{
    /// <summary>Command carrying the kid id and the new weekly slots.</summary>
    public sealed record Command(
        Guid MemberId,
        IReadOnlyList<SchoolDayScheduleSlotDto> Slots) : IRequest<Result<KidScheduleDto>>;

    /// <summary>Validation: each slot has at least one caretaker; weekdays unique.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Slots).NotNull();
            RuleForEach(x => x.Slots).ChildRules(slot =>
            {
                slot.RuleFor(s => s)
                    .Must(s => s.DropoffMemberId is not null || s.PickupMemberId is not null)
                    .WithMessage("Each slot must set at least one caretaker (drop-off or pick-up).");
            });
            RuleFor(x => x.Slots)
                .Must(slots => slots.Select(s => s.DayOfWeek).Distinct().Count() == slots.Count)
                .WithMessage("Duplicate weekday in the schedule.");
        }
    }

    /// <summary>Diffs the persisted schedule against the request and applies the delta.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<KidScheduleDto>>
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
        public async Task<Result<KidScheduleDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            // Validate the kid + every caretaker referenced — all must belong to this family.
            var memberIds = new HashSet<Guid> { request.MemberId };
            foreach (var slot in request.Slots)
            {
                if (slot.DropoffMemberId is { } d) memberIds.Add(d);
                if (slot.PickupMemberId is { } p) memberIds.Add(p);
            }

            var familyMembers = await _db.FamilyMembers
                .Where(m => memberIds.Contains(m.Id) && m.FamilyId == current.Family.Id)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            if (familyMembers.Count != memberIds.Count)
            {
                return ApplicationError.Validation(
                    "school.day_schedule.unknown_member",
                    "One or more referenced members are not part of this family.");
            }

            var existing = await _db.SchoolDaySchedules
                .Where(s => s.FamilyMemberId == request.MemberId)
                .ToListAsync(cancellationToken);

            var requested = request.Slots.ToDictionary(s => s.DayOfWeek);

            // Update overlapping rows, drop the ones that disappeared.
            foreach (var row in existing)
            {
                if (requested.TryGetValue(row.DayOfWeek, out var slot))
                {
                    row.DropoffMemberId = slot.DropoffMemberId;
                    row.PickupMemberId = slot.PickupMemberId;
                }
                else
                {
                    _db.SchoolDaySchedules.Remove(row);
                }
            }

            // Add brand-new weekday rows.
            var existingDays = existing.Select(r => r.DayOfWeek).ToHashSet();
            foreach (var slot in request.Slots)
            {
                if (existingDays.Contains(slot.DayOfWeek)) continue;
                _db.SchoolDaySchedules.Add(new SchoolDaySchedule
                {
                    FamilyMemberId = request.MemberId,
                    DayOfWeek = slot.DayOfWeek,
                    DropoffMemberId = slot.DropoffMemberId,
                    PickupMemberId = slot.PickupMemberId,
                });
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new KidScheduleDto(
                request.MemberId,
                request.Slots.OrderBy(s => s.DayOfWeek).ToList());
        }
    }
}
