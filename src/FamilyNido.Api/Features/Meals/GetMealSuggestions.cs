using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Meals;

/// <summary>
/// Slice: autocomplete suggestions from the family's meal-name history. Reads
/// the two course columns with two parallel SQL queries (each one filtered
/// case-insensitively in the database via ILIKE) and merges + dedupes them in
/// memory. Volume is small enough — even a year of history is ~100 rows — that
/// the in-memory step is cheaper than a tricky UNION/GROUP BY translation in EF.
/// </summary>
public static class GetMealSuggestions
{
    /// <summary>Default cap on results returned to the UI.</summary>
    public const int DefaultLimit = 8;

    /// <summary>Hard upper bound to avoid pathological queries.</summary>
    public const int MaxLimit = 50;

    /// <summary>Query — empty prefix returns the most recent meal names overall.</summary>
    public sealed record Query(string? Prefix, int? Limit) : IRequest<Result<IReadOnlyList<string>>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<string>>>
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
        public async Task<Result<IReadOnlyList<string>>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "No authenticated caller.");
            }

            var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);
            var prefix = (request.Prefix ?? string.Empty).Trim();
            var pattern = prefix.Length == 0 ? null : EscapeLikePattern(prefix) + "%";

            var familyId = current.Family.Id;

            // Two parallel-shaped queries — each pulls (name, lastUsed) for one
            // course column. Filter happens in SQL via ILIKE so the underlying
            // (family_id, first_course) and (family_id, second_course) indexes
            // can be used.
            var firsts = await _db.MealPlanSlots
                .AsNoTracking()
                .Where(s => s.FamilyId == familyId &&
                            s.FirstCourse != null &&
                            (pattern == null || EF.Functions.ILike(s.FirstCourse!, pattern)))
                .Select(s => new { Name = s.FirstCourse!, LastUsed = s.UpdatedAt ?? s.CreatedAt })
                .ToListAsync(cancellationToken);

            var seconds = await _db.MealPlanSlots
                .AsNoTracking()
                .Where(s => s.FamilyId == familyId &&
                            s.SecondCourse != null &&
                            (pattern == null || EF.Functions.ILike(s.SecondCourse!, pattern)))
                .Select(s => new { Name = s.SecondCourse!, LastUsed = s.UpdatedAt ?? s.CreatedAt })
                .ToListAsync(cancellationToken);

            // Merge in memory. A name appearing on both columns counts once with
            // its most recent activity.
            var suggestions = firsts
                .Concat(seconds)
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Name = g.Key, LastUsed = g.Max(x => x.LastUsed) })
                .OrderByDescending(g => g.LastUsed)
                .Take(limit)
                .Select(g => g.Name)
                .ToList();

            return Result<IReadOnlyList<string>>.Success(suggestions);
        }

        private static string EscapeLikePattern(string input)
        {
            // Postgres LIKE wildcards are %, _ and \. Escape so a user typing
            // "100%" does not match every record.
            return input
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
        }
    }
}
