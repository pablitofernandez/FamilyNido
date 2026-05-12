using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: revert a previous completion of a task occurrence (RF-TASK-003 + RF-TASK-007).</summary>
public static class UndoOccurrence
{
    /// <summary>Command carrying the target task and date.</summary>
    public sealed record Command(Guid TaskId, DateOnly OccurrenceDate)
        : IRequest<Result<TaskOccurrenceDto>>;

    /// <summary>Handler — removes the completion row.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<TaskOccurrenceDto>>
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
        public async Task<Result<TaskOccurrenceDto>> HandleAsync(
            Command request,
            CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
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

            var existing = task.Completions.FirstOrDefault();
            if (existing is null)
            {
                // Idempotent — undoing a not-yet-completed occurrence is a no-op.
                return TaskOccurrenceDto.From(task, request.OccurrenceDate, null);
            }

            _db.TaskCompletions.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);

            return TaskOccurrenceDto.From(task, request.OccurrenceDate, null);
        }
    }
}
