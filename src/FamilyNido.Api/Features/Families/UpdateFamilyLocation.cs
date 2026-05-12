using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Families;

/// <summary>
/// Slice for <c>PUT /api/family/location</c>. Lets a family admin set or
/// clear the family's geographic location. Drives the dashboard weather
/// widget — without coordinates, the widget hides itself.
/// </summary>
public static class UpdateFamilyLocation
{
    /// <summary>Command carrying the new location, or all nulls to clear it.</summary>
    /// <param name="Latitude">Decimal degrees in [-90, 90], or null to clear.</param>
    /// <param name="Longitude">Decimal degrees in [-180, 180], or null to clear.</param>
    /// <param name="LocationLabel">Optional human-readable label (city/town/village).</param>
    public sealed record Command(
        double? Latitude,
        double? Longitude,
        string? LocationLabel) : IRequest<Result<FamilyDto>>;

    /// <summary>Validation: lat/lon must be both null or both within range.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90)
                .When(x => x.Latitude is not null);

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180)
                .When(x => x.Longitude is not null);

            RuleFor(x => x)
                .Must(c => (c.Latitude is null) == (c.Longitude is null))
                .WithMessage("Latitude and Longitude must be set or cleared together.");

            RuleFor(x => x.LocationLabel).MaximumLength(120);
        }
    }

    /// <summary>Persists the new location on the family row.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<FamilyDto>>
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
        public async Task<Result<FamilyDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family.");
            }

            if (current.User.Role != FamilyRole.Admin)
            {
                return ApplicationError.Forbidden("family.location.admin_only", "Only family admins can change the location.");
            }

            var family = await _db.Families.FirstAsync(f => f.Id == current.Family.Id, cancellationToken);

            family.Latitude = request.Latitude;
            family.Longitude = request.Longitude;
            family.LocationLabel = string.IsNullOrWhiteSpace(request.LocationLabel) ? null : request.LocationLabel.Trim();

            await _db.SaveChangesAsync(cancellationToken);

            return new FamilyDto(
                family.Id,
                family.Name,
                family.TimeZone,
                family.Locale,
                family.Latitude,
                family.Longitude,
                family.LocationLabel);
        }
    }
}
