using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>PATCH /api/school/extracurriculars/{id}/archive</c>. Marks
/// the activity as archived (soft delete) so the history of past courses is
/// preserved. Re-call to flip back via the <see cref="Command.IsArchived"/>
/// flag.
/// </summary>
public static class ArchiveExtracurricular
{
    /// <summary>Command to flip the archive flag.</summary>
    public sealed record Command(Guid Id, bool IsArchived) : IRequest<Result<ExtracurricularDto>>;

    /// <summary>Updates the flag after family-scope check.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<ExtracurricularDto>>
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
        public async Task<Result<ExtracurricularDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var entry = await _db.Extracurriculars.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
            if (entry is null || entry.FamilyId != current.Family.Id)
            {
                return ApplicationError.NotFound("school.extracurricular.not_found", $"Activity {request.Id} not found.");
            }

            entry.IsArchived = request.IsArchived;
            await _db.SaveChangesAsync(cancellationToken);
            return AddExtracurricular.Handler.ToDto(entry);
        }
    }
}
