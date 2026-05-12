using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: fetch one task by id, scoped to the caller's family.</summary>
public static class GetHouseholdTask
{
    /// <summary>Query carrying the target id.</summary>
    public sealed record Query(Guid TaskId) : IRequest<Result<HouseholdTaskDto>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<HouseholdTaskDto>>
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
        public async Task<Result<HouseholdTaskDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var task = await _db.HouseholdTasks
                .AsNoTracking()
                .Include(t => t.RelatedMembers)
                .FirstOrDefaultAsync(
                    t => t.Id == request.TaskId && t.FamilyId == current.Family.Id,
                    cancellationToken);

            return task is null
                ? ApplicationError.NotFound("household_task.not_found", $"Task {request.TaskId} not found.")
                : HouseholdTaskDto.From(task);
        }
    }
}
