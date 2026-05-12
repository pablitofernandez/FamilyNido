using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Calendar;

/// <summary>Slice: list every Google account linked under the current family.</summary>
public static class ListLinkedAccounts
{
    /// <summary>Empty query.</summary>
    public sealed record Query() : IRequest<Result<IReadOnlyList<GoogleAccountDto>>>;

    /// <summary>Handler — returns all family accounts (every adult sees the family-wide picture).</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<GoogleAccountDto>>>
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
        public async Task<Result<IReadOnlyList<GoogleAccountDto>>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var accounts = await _db.GoogleAccounts
                .AsNoTracking()
                .Where(a => a.FamilyId == current.Family.Id)
                .Include(a => a.Calendars)
                .OrderBy(a => a.Email)
                .ToListAsync(cancellationToken);

            IReadOnlyList<GoogleAccountDto> dto = [.. accounts.Select(GoogleAccountDto.From)];
            return Result<IReadOnlyList<GoogleAccountDto>>.Success(dto);
        }
    }
}
