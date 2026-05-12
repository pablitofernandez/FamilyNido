using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Agenda;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.MemberAgenda;

/// <summary>
/// Slice for <c>POST /api/member-agenda/exceptions</c> + <c>PUT
/// /api/member-agenda/exceptions/{id}</c>. Upserts a per-date exception:
/// either an override of an existing pattern (PatternId set) or an ad-hoc
/// one-off entry (PatternId null).
/// </summary>
public static class SetMemberAgendaException
{
    /// <summary>
    /// Command. <paramref name="Id"/> null = create; non-null = update that
    /// row in place. For pattern overrides, <paramref name="MemberId"/> +
    /// <paramref name="Date"/> + <paramref name="PatternId"/> uniquely
    /// identify the row, but having an explicit id keeps the URL clean.
    /// </summary>
    public sealed record Command(
        Guid? Id,
        Guid MemberId,
        DateOnly Date,
        Guid? PatternId,
        bool IsCancelled,
        string? Label,
        string? Location,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        AgendaTransportMode? TransportMode,
        bool? IsAway,
        string? Notes) : IRequest<Result<MemberAgendaExceptionDto>>;

    /// <summary>Validation: ad-hoc entries can't be cancellations and must have a label; time order sane.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Label).MaximumLength(120);
            RuleFor(x => x.Location).MaximumLength(200);
            RuleFor(x => x.Notes).MaximumLength(500);
            RuleFor(x => x).Must(c => !(c.PatternId is null && c.IsCancelled))
                .WithMessage("Ad-hoc entries cannot be cancellations.");
            RuleFor(x => x).Must(c => c.PatternId is not null || !string.IsNullOrWhiteSpace(c.Label))
                .WithMessage("Ad-hoc entries must carry a label.");
            RuleFor(x => x).Must(c => !(c.StartTime is { } s && c.EndTime is { } e) || e >= s)
                .WithMessage("End time must be on or after start time.");
        }
    }

    /// <summary>Persists the upsert.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<MemberAgendaExceptionDto>>
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
        public async Task<Result<MemberAgendaExceptionDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (!MemberAgendaPermissions.CanWrite(current, request.MemberId))
            {
                return ApplicationError.Forbidden("agenda.forbidden", "Only an admin or the member themselves can edit this agenda.");
            }

            var familyId = current!.Family.Id;

            var memberOk = await _db.FamilyMembers
                .AnyAsync(m => m.Id == request.MemberId && m.FamilyId == familyId, cancellationToken);
            if (!memberOk)
            {
                return ApplicationError.Validation("agenda.unknown_member", "Member does not belong to this family.");
            }

            // When overriding a pattern, ensure the pattern exists and belongs to the same member.
            if (request.PatternId is { } pid)
            {
                var patternBelongs = await _db.MemberAgendaPatterns
                    .AnyAsync(p => p.Id == pid && p.FamilyMemberId == request.MemberId && p.FamilyId == familyId, cancellationToken);
                if (!patternBelongs)
                {
                    return ApplicationError.Validation("agenda.unknown_pattern", "Pattern does not belong to this member.");
                }
            }

            MemberAgendaException? entity;
            if (request.Id is { } id)
            {
                entity = await _db.MemberAgendaExceptions
                    .FirstOrDefaultAsync(e => e.Id == id && e.FamilyId == familyId, cancellationToken);
                if (entity is null)
                {
                    return ApplicationError.NotFound("agenda.exception_not_found", "Exception not found.");
                }
            }
            else
            {
                entity = new MemberAgendaException
                {
                    FamilyId = familyId,
                    FamilyMemberId = request.MemberId,
                    Date = request.Date,
                    PatternId = request.PatternId,
                };
                _db.MemberAgendaExceptions.Add(entity);
            }

            entity.IsCancelled = request.IsCancelled;
            entity.Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
            entity.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
            entity.StartTime = request.StartTime;
            entity.EndTime = request.EndTime;
            entity.TransportMode = request.TransportMode;
            entity.IsAway = request.IsAway;
            entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            await _db.SaveChangesAsync(cancellationToken);

            return new MemberAgendaExceptionDto(
                entity.Id, entity.FamilyMemberId, entity.Date, entity.PatternId, entity.IsCancelled,
                entity.Label, entity.Location, entity.StartTime, entity.EndTime,
                entity.TransportMode, entity.IsAway, entity.Notes);
        }
    }
}
