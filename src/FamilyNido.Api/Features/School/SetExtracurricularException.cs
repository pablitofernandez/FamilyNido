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
/// Slice for <c>PUT /api/school/extracurriculars/{id}/exceptions/{date}</c>.
/// Upserts a per-session override or cancellation. Mirror of
/// <see cref="SetSchoolDayException"/> for the activities side.
/// </summary>
public static class SetExtracurricularException
{
    /// <summary>Command carrying the (activity, date) tuple and the new state.</summary>
    public sealed record Command(
        Guid ExtracurricularId,
        DateOnly Date,
        bool IsCancelled,
        Guid? DropoffMemberId,
        Guid? PickupMemberId,
        string? Notes) : IRequest<Result<ExtracurricularExceptionDto>>;

    /// <summary>Validation: cancellation OR an actual change must be present.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Notes).MaximumLength(500);
            RuleFor(x => x)
                .Must(c => c.IsCancelled || c.DropoffMemberId is not null || c.PickupMemberId is not null || !string.IsNullOrWhiteSpace(c.Notes))
                .WithMessage("Provide at least one change (cancel, override drop-off / pick-up, or a note).");
        }
    }

    /// <summary>Inserts or updates the exception row.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<ExtracurricularExceptionDto>>
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
        public async Task<Result<ExtracurricularExceptionDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var activity = await _db.Extracurriculars.FirstOrDefaultAsync(e => e.Id == request.ExtracurricularId, cancellationToken);
            if (activity is null || activity.FamilyId != current.Family.Id)
            {
                return ApplicationError.NotFound("school.extracurricular.not_found", $"Activity {request.ExtracurricularId} not found.");
            }

            var ids = new List<Guid>();
            if (request.DropoffMemberId is { } d) ids.Add(d);
            if (request.PickupMemberId is { } p) ids.Add(p);
            if (ids.Count > 0)
            {
                var found = await _db.FamilyMembers
                    .Where(m => ids.Contains(m.Id) && m.FamilyId == current.Family.Id)
                    .CountAsync(cancellationToken);
                if (found != ids.Distinct().Count())
                {
                    return ApplicationError.Validation(
                        "school.extracurricular.unknown_member",
                        "Caretaker referenced is not part of this family.");
                }
            }

            var entry = await _db.ExtracurricularExceptions
                .FirstOrDefaultAsync(x => x.ExtracurricularId == request.ExtracurricularId && x.Date == request.Date, cancellationToken);

            if (entry is null)
            {
                entry = new ExtracurricularException
                {
                    ExtracurricularId = request.ExtracurricularId,
                    Date = request.Date,
                };
                _db.ExtracurricularExceptions.Add(entry);
            }

            entry.IsCancelled = request.IsCancelled;
            entry.DropoffMemberId = request.IsCancelled ? null : request.DropoffMemberId;
            entry.PickupMemberId = request.IsCancelled ? null : request.PickupMemberId;
            entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            await _db.SaveChangesAsync(cancellationToken);

            return new ExtracurricularExceptionDto(
                entry.Id,
                entry.ExtracurricularId,
                entry.Date,
                entry.IsCancelled,
                entry.DropoffMemberId,
                entry.PickupMemberId,
                entry.Notes);
        }
    }
}
