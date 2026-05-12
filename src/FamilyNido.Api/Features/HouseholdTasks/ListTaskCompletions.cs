using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>
/// Slice: full per-occurrence completion history for a single task, sorted
/// most recent first. Powers the "Historial" panel in the task form so
/// admins can see and (via the existing PUT endpoint) re-attribute who did
/// the chore on any past day — particularly useful for daily/weekly
/// recurring tasks where the latest completion alone isn't enough.
/// </summary>
public static class ListTaskCompletions
{
    /// <summary>Query carrying the target task id.</summary>
    public sealed record Query(Guid TaskId) : IRequest<Result<IReadOnlyList<TaskCompletionEntryDto>>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<TaskCompletionEntryDto>>>
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
        public async Task<Result<IReadOnlyList<TaskCompletionEntryDto>>> HandleAsync(
            Query request,
            CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            // Only callers from the task's family can see history.
            var taskExists = await _db.HouseholdTasks.AnyAsync(
                t => t.Id == request.TaskId && t.FamilyId == current.Family.Id,
                cancellationToken);
            if (!taskExists)
            {
                return ApplicationError.NotFound("household_task.not_found", $"Task {request.TaskId} not found.");
            }

            var entries = await _db.TaskCompletions
                .AsNoTracking()
                .Where(c => c.TaskId == request.TaskId)
                .OrderByDescending(c => c.OccurrenceDate)
                .Select(c => new TaskCompletionEntryDto(
                    c.OccurrenceDate,
                    c.CompletedByMemberId,
                    c.CompletedAt,
                    c.Note))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<TaskCompletionEntryDto>>.Success(entries);
        }
    }
}

/// <summary>One row in the per-task completion history.</summary>
/// <param name="OccurrenceDate">Date this completion is for (key with TaskId).</param>
/// <param name="CompletedByMemberId">Member credited as the completer, or null if anonymous.</param>
/// <param name="CompletedAt">UTC instant the click happened.</param>
/// <param name="Note">Optional note left by the completer.</param>
public sealed record TaskCompletionEntryDto(
    DateOnly OccurrenceDate,
    Guid? CompletedByMemberId,
    DateTimeOffset CompletedAt,
    string? Note);
