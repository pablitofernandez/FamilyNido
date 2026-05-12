using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: hard-delete a task. Only the creator or a family admin may delete (RF-TASK-004).</summary>
public static class DeleteHouseholdTask
{
    /// <summary>Command carrying the target id.</summary>
    public sealed record Command(Guid TaskId) : IRequest<Result<Unit>>;

    /// <summary>Handler — enforces the creator-or-admin rule before deleting.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Unit>>
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
        public async Task<Result<Unit>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var task = await _db.HouseholdTasks
                .FirstOrDefaultAsync(
                    t => t.Id == request.TaskId && t.FamilyId == current.Family.Id,
                    cancellationToken);

            if (task is null)
            {
                return ApplicationError.NotFound("household_task.not_found", $"Task {request.TaskId} not found.");
            }

            var isAdmin = current.User.Role == FamilyRole.Admin;
            var isCreator = current.Member.Id == task.CreatedByMemberId;
            if (!isAdmin && !isCreator)
            {
                return ApplicationError.Forbidden(
                    "household_task.only_creator_or_admin_can_delete",
                    "Only the creator or a family admin may delete a task.");
            }

            _db.HouseholdTasks.Remove(task);
            await _db.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
