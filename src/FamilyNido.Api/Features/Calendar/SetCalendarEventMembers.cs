using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>
/// Slice: replace the set of family members tagged on a calendar event. The
/// API is set-based (not add/remove) because the editor in the UI shows the
/// full picker and posts the selection in one call — easier to reason about
/// than incremental diffs (RF-CAL-013).
/// </summary>
public static class SetCalendarEventMembers
{
    /// <summary>Command.</summary>
    /// <param name="EventId">Local event id (FamilyNido <c>CalendarEvent.Id</c>).</param>
    /// <param name="MemberIds">New set of related members. Empty clears the relation.</param>
    public sealed record Command(Guid EventId, IReadOnlyList<Guid> MemberIds)
        : IRequest<Result<CalendarEventDto>>;

    /// <summary>Validator.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator.</summary>
        public Validator()
        {
            RuleFor(x => x.EventId).NotEmpty();
            RuleFor(x => x.MemberIds).NotNull();
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<CalendarEventDto>>
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
        public async Task<Result<CalendarEventDto>> HandleAsync(Command request, CancellationToken ct)
        {
            var current = await _userContext.GetAsync(ct);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var ev = await _db.CalendarEvents
                .Include(e => e.LinkedCalendar)
                .Include(e => e.RelatedMembers)
                .FirstOrDefaultAsync(
                    e => e.Id == request.EventId && e.FamilyId == current.Family.Id,
                    ct);

            if (ev is null)
            {
                return ApplicationError.NotFound("calendar_event.not_found", "Event not found.");
            }

            var newIds = request.MemberIds.ToHashSet();
            var newMembers = newIds.Count == 0
                ? []
                : await _db.FamilyMembers
                    .Where(m => m.FamilyId == current.Family.Id && newIds.Contains(m.Id))
                    .ToListAsync(ct);

            if (newMembers.Count != newIds.Count)
            {
                return ApplicationError.Validation(
                    "calendar_event.unknown_member",
                    "One or more referenced members are not part of this family.");
            }

            // Replace the set in-memory; EF persists the join changes on save.
            ev.RelatedMembers.Clear();
            foreach (var m in newMembers)
            {
                ev.RelatedMembers.Add(m);
            }

            await _db.SaveChangesAsync(ct);

            return CalendarEventDto.From(ev);
        }
    }
}
