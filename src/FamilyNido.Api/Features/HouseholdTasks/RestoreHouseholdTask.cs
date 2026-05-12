using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: reverse <see cref="ArchiveHouseholdTask"/> (RF-TASK-005).</summary>
public static class RestoreHouseholdTask
{
    /// <summary>Command carrying the target id.</summary>
    public sealed record Command(Guid TaskId) : IRequest<Result<HouseholdTaskDto>>;

    /// <summary>Handler — flips IsArchived to false.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<HouseholdTaskDto>>
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
        public async Task<Result<HouseholdTaskDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var task = await _db.HouseholdTasks
                .Include(t => t.RelatedMembers)
                .FirstOrDefaultAsync(
                    t => t.Id == request.TaskId && t.FamilyId == current.Family.Id,
                    cancellationToken);

            if (task is null)
            {
                return ApplicationError.NotFound("household_task.not_found", $"Task {request.TaskId} not found.");
            }

            task.IsArchived = false;
            await _db.SaveChangesAsync(cancellationToken);

            return HouseholdTaskDto.From(task);
        }
    }
}
