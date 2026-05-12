using System.Text.Json;
using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Notifications;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Dashboard;

/// <summary>
/// Slice for <c>PUT /api/dashboard/preferences</c>. Replaces the user's widget
/// layout with the supplied list. The persisted JSON is then re-reconciled on
/// read, so unknown ids the caller may slip in are simply ignored next time.
/// </summary>
public static class UpdateMyDashboardPreferences
{
    /// <summary>Command carrying the full ordered widget list.</summary>
    public sealed record Command(IReadOnlyList<DashboardWidgetDto> Widgets)
        : IRequest<Result<DashboardPreferencesDto>>;

    /// <summary>Validation: every id must be from the catalogue and unique.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Widgets).NotNull();
            RuleForEach(x => x.Widgets)
                .Must(w => DashboardWidgets.IsKnown(w.Id))
                .WithMessage("Unknown widget id.");
            RuleFor(x => x.Widgets)
                .Must(list => list.Select(w => w.Id).Distinct().Count() == list.Count)
                .WithMessage("Duplicate widget id.");
        }
    }

    /// <summary>Performs the upsert.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<DashboardPreferencesDto>>
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
        public async Task<Result<DashboardPreferencesDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var user = await _userContext.GetUserAsync(cancellationToken);
            if (user is null)
            {
                return ApplicationError.Forbidden("auth.unauthenticated", "Not signed in.");
            }

            // Serialise the requested layout — Reconcile() will trim unknowns
            // on the next read, so what we persist is exactly what we accept.
            var json = JsonSerializer.Serialize(request.Widgets);

            var prefs = await _db.UserDashboardPreferences
                .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
            if (prefs is null)
            {
                prefs = new UserDashboardPreferences { UserId = user.Id, WidgetsJson = json };
                _db.UserDashboardPreferences.Add(prefs);
            }
            else
            {
                prefs.WidgetsJson = json;
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new DashboardPreferencesDto(GetMyDashboardPreferences.Handler.Reconcile(json));
        }
    }
}
