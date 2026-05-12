using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>DELETE /api/school/extracurriculars/{id}</c>. Hard-deletes
/// the row along with its exceptions (cascade). Reserve for clean-up; the
/// usual lifecycle ends with <see cref="ArchiveExtracurricular"/>.
/// </summary>
public static class DeleteExtracurricular
{
    /// <summary>Command identifying the row.</summary>
    public sealed record Command(Guid Id) : IRequest<Result<Unit>>;

    /// <summary>Empty success payload.</summary>
    public sealed record Unit;

    /// <summary>Removes the row after verifying family scope.</summary>
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

            var entry = await _db.Extracurriculars.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
            if (entry is null || entry.FamilyId != current.Family.Id)
            {
                return ApplicationError.NotFound("school.extracurricular.not_found", $"Activity {request.Id} not found.");
            }

            _db.Extracurriculars.Remove(entry);
            await _db.SaveChangesAsync(cancellationToken);
            return new Unit();
        }
    }
}
