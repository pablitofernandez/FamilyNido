using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.MemberAgenda;

/// <summary>Slice for <c>DELETE /api/member-agenda/exceptions/{id}</c>.</summary>
public static class DeleteMemberAgendaException
{
    /// <summary>Command carrying the exception id.</summary>
    public sealed record Command(Guid Id) : IRequest<Result<Unit>>;

    /// <summary>Removes the row.</summary>
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

            var entity = await _db.MemberAgendaExceptions
                .FirstOrDefaultAsync(e => e.Id == request.Id && e.FamilyId == current.Family.Id, cancellationToken);
            if (entity is null)
            {
                return Unit.Value;
            }
            if (!MemberAgendaPermissions.CanWrite(current, entity.FamilyMemberId))
            {
                return ApplicationError.Forbidden("agenda.forbidden", "Only an admin or the member themselves can edit this agenda.");
            }

            _db.MemberAgendaExceptions.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
