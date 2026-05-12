using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.School;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.School;

/// <summary>
/// Slice for <c>PUT /api/school/members/{memberId}/profile</c>. Lazily creates
/// the row on first save, then updates in place. All free-text fields are
/// nullable so a minimal "ficha" is allowed.
/// </summary>
public static class UpsertSchoolProfile
{
    /// <summary>Replaces the kid's school profile in full.</summary>
    public sealed record Command(
        Guid MemberId,
        string? SchoolName,
        string? Grade,
        string? Tutor,
        TransportMode TransportMode,
        TimeOnly? MorningTime,
        TimeOnly? AfternoonTime,
        string? Notes) : IRequest<Result<SchoolProfileDto>>;

    /// <summary>Validation: cap each free-text field to the persisted length.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.SchoolName).MaximumLength(120);
            RuleFor(x => x.Grade).MaximumLength(60);
            RuleFor(x => x.Tutor).MaximumLength(120);
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleFor(x => x.TransportMode).IsInEnum();
        }
    }

    /// <summary>Performs the upsert with family-scope validation.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<SchoolProfileDto>>
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
        public async Task<Result<SchoolProfileDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var memberOk = await _db.FamilyMembers
                .AnyAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, cancellationToken);
            if (!memberOk)
            {
                return ApplicationError.NotFound("family_member.not_found", $"Member {request.MemberId} not found.");
            }

            var profile = await _db.SchoolProfiles
                .FirstOrDefaultAsync(p => p.FamilyMemberId == request.MemberId, cancellationToken);
            if (profile is null)
            {
                profile = new SchoolProfile { FamilyMemberId = request.MemberId };
                _db.SchoolProfiles.Add(profile);
            }

            profile.SchoolName = Trim(request.SchoolName);
            profile.Grade = Trim(request.Grade);
            profile.Tutor = Trim(request.Tutor);
            profile.TransportMode = request.TransportMode;
            profile.MorningTime = request.MorningTime;
            profile.AfternoonTime = request.AfternoonTime;
            profile.Notes = Trim(request.Notes);

            await _db.SaveChangesAsync(cancellationToken);

            return new SchoolProfileDto(
                profile.SchoolName,
                profile.Grade,
                profile.Tutor,
                profile.TransportMode,
                profile.MorningTime,
                profile.AfternoonTime,
                profile.Notes);
        }

        private static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
