using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>
/// Slice: admin-only upsert of the completion record for a single occurrence,
/// attributing it to a chosen family member. Used to fix mistakes ("I clicked
/// done but Dan really did it") and to mark something as done on behalf of
/// somebody else.
/// </summary>
/// <remarks>
/// Endpoint sits at <c>PUT .../occurrences/{date}/completion</c> alongside the
/// existing <c>POST .../complete</c> and <c>POST .../undo</c>. The PUT verb
/// matches the "idempotent upsert" semantics: if no completion exists for the
/// occurrence we create one with <c>CompletedAt = now</c>; if one exists we
/// update its attribution (and the optional note) but keep the original
/// <c>CompletedAt</c> so the audit trail stays honest about *when* the task
/// was first marked done.
/// </remarks>
public static class SetCompletionAttribution
{
    /// <summary>Command carrying the new attribution and an optional note.</summary>
    /// <param name="TaskId">Owning task.</param>
    /// <param name="OccurrenceDate">Date of the occurrence being attributed.</param>
    /// <param name="CompletedByMemberId">Member who should appear as the completer.</param>
    /// <param name="Note">Optional free-text note (replaces any prior note).</param>
    public sealed record Command(
        Guid TaskId,
        DateOnly OccurrenceDate,
        Guid CompletedByMemberId,
        string? Note) : IRequest<Result<TaskOccurrenceDto>>;

    /// <summary>Validator — rejects the empty member id at the wire level.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.CompletedByMemberId).NotEmpty();
            RuleFor(x => x.Note).MaximumLength(500);
        }
    }

    /// <summary>Handler — admin-only via endpoint policy; double-checks role here too.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<TaskOccurrenceDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _clock;

        /// <summary>Primary constructor.</summary>
        public Handler(ApplicationDbContext db, ICurrentUserContext userContext, TimeProvider clock)
        {
            _db = db;
            _userContext = userContext;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<Result<TaskOccurrenceDto>> HandleAsync(
            Command request,
            CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            // Endpoint already gates on the Admin policy, but the handler is
            // also reachable from tests that bypass the policy — defend in
            // depth so a future refactor doesn't accidentally widen access.
            if (current.User.Role != FamilyRole.Admin)
            {
                return ApplicationError.Forbidden(
                    "household_task.attribution_admin_only",
                    "Only admins can change completion attribution.");
            }

            var task = await _db.HouseholdTasks
                .Include(t => t.Completions.Where(c => c.OccurrenceDate == request.OccurrenceDate))
                .FirstOrDefaultAsync(
                    t => t.Id == request.TaskId && t.FamilyId == current.Family.Id,
                    cancellationToken);

            if (task is null)
            {
                return ApplicationError.NotFound("household_task.not_found", $"Task {request.TaskId} not found.");
            }

            if (task.IsArchived)
            {
                return ApplicationError.Conflict(
                    "household_task.archived",
                    "Archived tasks cannot be completed; restore them first.");
            }

            if (!task.IsFloating && !task.HasOccurrenceOn(request.OccurrenceDate))
            {
                return ApplicationError.Validation(
                    "household_task.no_such_occurrence",
                    $"Task {task.Id} is not scheduled on {request.OccurrenceDate:yyyy-MM-dd}.");
            }

            // The new completer must be an active member of the same family —
            // anything else would let an admin "credit" a person from another
            // household, which makes no sense and breaks scoreboard math.
            var memberExists = await _db.FamilyMembers.AnyAsync(
                m => m.Id == request.CompletedByMemberId
                    && m.FamilyId == current.Family.Id
                    && m.IsActive,
                cancellationToken);
            if (!memberExists)
            {
                return ApplicationError.Validation(
                    "household_task.unknown_member",
                    $"Member {request.CompletedByMemberId} is not an active member of this family.");
            }

            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note;
            var existing = task.Completions.FirstOrDefault();

            if (existing is null)
            {
                _db.TaskCompletions.Add(new TaskCompletion
                {
                    TaskId = task.Id,
                    OccurrenceDate = request.OccurrenceDate,
                    CompletedByMemberId = request.CompletedByMemberId,
                    CompletedAt = _clock.GetUtcNow(),
                    Note = note,
                });
            }
            else
            {
                existing.CompletedByMemberId = request.CompletedByMemberId;
                existing.Note = note;
                // CompletedAt stays — we're correcting attribution, not
                // overwriting when the task was actually done.
            }

            await _db.SaveChangesAsync(cancellationToken);

            // Re-read so the DTO sees the freshly persisted state.
            var fresh = await _db.TaskCompletions.AsNoTracking().FirstOrDefaultAsync(
                c => c.TaskId == task.Id && c.OccurrenceDate == request.OccurrenceDate,
                cancellationToken);
            return TaskOccurrenceDto.From(task, request.OccurrenceDate, fresh);
        }
    }
}
