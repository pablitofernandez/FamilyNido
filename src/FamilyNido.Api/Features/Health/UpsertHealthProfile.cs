using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Health;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.Health;

/// <summary>
/// Slice for <c>PUT /api/health/members/{memberId}/profile</c>. Lazily creates
/// the row on first save, then updates in place. Free-text fields are trimmed
/// and converted to null when blank.
/// </summary>
public static class UpsertHealthProfile
{
    /// <summary>Replace the member's profile in full.</summary>
    public sealed record Command(
        Guid MemberId,
        string? BloodType,
        string? Allergies,
        string? ChronicConditions,
        string? Notes) : IRequest<Result<HealthProfileDto>>;

    /// <summary>Validation: cap each field to its persisted length.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.BloodType).MaximumLength(8);
            RuleFor(x => x.Allergies).MaximumLength(2000);
            RuleFor(x => x.ChronicConditions).MaximumLength(2000);
            RuleFor(x => x.Notes).MaximumLength(4000);
        }
    }

    /// <summary>Performs the upsert with permission gating.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<HealthProfileDto>>
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
        public async Task<Result<HealthProfileDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var member = await _db.FamilyMembers
                .FirstOrDefaultAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, cancellationToken);
            if (member is null)
            {
                return ApplicationError.NotFound("family_member.not_found", $"Member {request.MemberId} not found.");
            }

            var profile = await _db.HealthProfiles
                .FirstOrDefaultAsync(p => p.FamilyMemberId == request.MemberId, cancellationToken);
            if (profile is null)
            {
                profile = new HealthProfile { FamilyMemberId = request.MemberId };
                _db.HealthProfiles.Add(profile);
            }

            profile.BloodType = Trim(request.BloodType);
            profile.Allergies = Trim(request.Allergies);
            profile.ChronicConditions = Trim(request.ChronicConditions);
            profile.Notes = Trim(request.Notes);

            await _db.SaveChangesAsync(cancellationToken);

            return new HealthProfileDto(profile.BloodType, profile.Allergies, profile.ChronicConditions, profile.Notes);
        }

        private static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
