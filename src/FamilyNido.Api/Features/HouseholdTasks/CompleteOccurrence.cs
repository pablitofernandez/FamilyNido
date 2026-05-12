using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: mark a specific occurrence of a task as done (RF-TASK-003 + RF-TASK-007).</summary>
public static class CompleteOccurrence
{
    /// <summary>Command carrying the target task, date and optional note.</summary>
    public sealed record Command(Guid TaskId, DateOnly OccurrenceDate, string? Note)
        : IRequest<Result<TaskOccurrenceDto>>;

    /// <summary>Handler — persists the completion.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<TaskOccurrenceDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly TimeProvider _clock;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            TimeProvider clock)
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

            // Floating tasks bypass the schedule check — they're "do me whenever"
            // chores and the completion date is whatever day the user actually did
            // them. Recurring/single-shot tasks still must own that occurrence.
            if (!task.IsFloating && !task.HasOccurrenceOn(request.OccurrenceDate))
            {
                return ApplicationError.Validation(
                    "household_task.no_such_occurrence",
                    $"Task {task.Id} is not scheduled on {request.OccurrenceDate:yyyy-MM-dd}.");
            }

            var existing = task.Completions.FirstOrDefault();
            if (existing is not null)
            {
                // Idempotent — return the existing completion.
                return TaskOccurrenceDto.From(task, request.OccurrenceDate, existing);
            }

            var completion = new TaskCompletion
            {
                TaskId = task.Id,
                OccurrenceDate = request.OccurrenceDate,
                CompletedByMemberId = current.Member.Id,
                CompletedAt = _clock.GetUtcNow(),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note,
            };

            _db.TaskCompletions.Add(completion);
            await _db.SaveChangesAsync(cancellationToken);

            return TaskOccurrenceDto.From(task, request.OccurrenceDate, completion);
        }
    }
}
