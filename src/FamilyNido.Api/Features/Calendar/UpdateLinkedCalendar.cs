using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Slice: toggle the import flag and/or assign a family member to a calendar. Toggling
/// import off purges the cached events immediately so the UI does not keep stale rows.
/// </summary>
public static class UpdateLinkedCalendar
{
    /// <summary>Command — partial update of a linked calendar.</summary>
    /// <param name="LinkedCalendarId">Id of the linked calendar to update.</param>
    /// <param name="IsImported">New value for the import flag.</param>
    /// <param name="FamilyMemberId">Optional family member to associate (null clears the assignment).</param>
    public sealed record Command(
        Guid LinkedCalendarId,
        bool IsImported,
        Guid? FamilyMemberId) : IRequest<Result<LinkedCalendarDto>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator.</summary>
        public Validator()
        {
            RuleFor(x => x.LinkedCalendarId).NotEmpty();
        }
    }

    /// <summary>Handler — applies the patch and saves.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<LinkedCalendarDto>>
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
        public async Task<Result<LinkedCalendarDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var calendar = await _db.LinkedCalendars
                .Include(c => c.GoogleAccount)
                .FirstOrDefaultAsync(
                    c => c.Id == request.LinkedCalendarId &&
                         c.GoogleAccount!.FamilyId == current.Family.Id,
                    cancellationToken);

            if (calendar is null)
            {
                return ApplicationError.NotFound(
                    "calendar.linked_calendar_not_found",
                    "El calendario indicado no existe o no pertenece a tu familia.");
            }

            if (request.FamilyMemberId is { } memberId)
            {
                var memberExists = await _db.FamilyMembers
                    .AnyAsync(m => m.Id == memberId && m.FamilyId == current.Family.Id, cancellationToken);
                if (!memberExists)
                {
                    return ApplicationError.Validation(
                        "calendar.member_not_found",
                        "El miembro familiar indicado no existe.");
                }
            }

            var willTurnOff = calendar.IsImported && !request.IsImported;

            calendar.IsImported = request.IsImported;
            calendar.FamilyMemberId = request.FamilyMemberId;

            // Toggling import off clears cached events and the sync token so a future
            // toggle-on triggers a fresh full sync rather than a delta from a stale cursor.
            if (willTurnOff)
            {
                await _db.CalendarEvents
                    .Where(e => e.LinkedCalendarId == calendar.Id)
                    .ExecuteDeleteAsync(cancellationToken);
                calendar.SyncToken = null;
                calendar.LastSyncedAt = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return LinkedCalendarDto.From(calendar);
        }
    }
}
