using System.Text.Json;
using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Dashboard;

/// <summary>
/// Slice for <c>GET /api/dashboard/preferences</c>. Reconciles the persisted
/// JSON with the catalogue of known widgets so:
/// <list type="bullet">
///   <item>Widgets the user explicitly hid stay hidden.</item>
///   <item>Widgets removed from the catalogue silently disappear.</item>
///   <item>New widgets added to the catalogue land at the end, visible by default.</item>
/// </list>
/// The result is the layout the frontend renders straight away.
/// </summary>
public static class GetMyDashboardPreferences
{
    /// <summary>Query carries no payload — the caller is the implicit subject.</summary>
    public sealed record Query : IRequest<Result<DashboardPreferencesDto>>;

    /// <summary>Reads + reconciles the row.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<DashboardPreferencesDto>>
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
        public async Task<Result<DashboardPreferencesDto>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var user = await _userContext.GetUserAsync(cancellationToken);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Not signed in.");
            }

            var stored = await _db.UserDashboardPreferences
                .AsNoTracking()
                .Where(p => p.UserId == user.Id)
                .Select(p => p.WidgetsJson)
                .FirstOrDefaultAsync(cancellationToken);

            return new DashboardPreferencesDto(Reconcile(stored));
        }

        /// <summary>
        /// Build the effective widget list: persisted entries are kept in their
        /// stored order (filtering out IDs no longer in the catalogue), and any
        /// new catalogue entry is appended at the end with <c>Visible=true</c>.
        /// </summary>
        internal static IReadOnlyList<DashboardWidgetDto> Reconcile(string? widgetsJson)
        {
            var stored = ParseStored(widgetsJson);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<DashboardWidgetDto>();

            foreach (var entry in stored)
            {
                if (!DashboardWidgets.IsKnown(entry.Id) || !seen.Add(entry.Id)) continue;
                result.Add(entry);
            }

            foreach (var id in DashboardWidgets.DefaultOrder)
            {
                if (seen.Add(id))
                {
                    result.Add(new DashboardWidgetDto(id, Visible: true));
                }
            }

            return result;
        }

        private static IReadOnlyList<DashboardWidgetDto> ParseStored(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                var parsed = JsonSerializer.Deserialize<List<DashboardWidgetDto>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return parsed ?? [];
            }
            catch (JsonException)
            {
                // Corrupted column — treat as defaults rather than failing the whole API.
                return [];
            }
        }
    }
}
