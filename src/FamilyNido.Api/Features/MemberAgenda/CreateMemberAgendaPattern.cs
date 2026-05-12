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
/// Slice for <c>POST /api/member-agenda/patterns</c>. Creates a recurring
/// weekly entry for a member. Admin can target any member; non-admins can
/// only target themselves.
/// </summary>
public static class CreateMemberAgendaPattern
{
    /// <summary>Command carrying the new pattern fields.</summary>
    public sealed record Command(
        Guid MemberId,
        DayOfWeek DayOfWeek,
        string Label,
        string? Location,
        TimeOnly? StartTime,
        TimeOnly? EndTime,
        AgendaTransportMode TransportMode,
        bool IsAway,
        string? Notes,
        bool IsActive) : IRequest<Result<MemberAgendaPatternDto>>;

    /// <summary>Validation: label required, time order sane.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Label).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Location).MaximumLength(200);
            RuleFor(x => x.Notes).MaximumLength(500);
            RuleFor(x => x).Must(c => !(c.StartTime is { } s && c.EndTime is { } e) || e >= s)
                .WithMessage("End time must be on or after start time.");
        }
    }

    /// <summary>Persists the row.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<MemberAgendaPatternDto>>
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
        public async Task<Result<MemberAgendaPatternDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (!MemberAgendaPermissions.CanWrite(current, request.MemberId))
            {
                return ApplicationError.Forbidden("agenda.forbidden", "Only an admin or the member themselves can edit this agenda.");
            }

            var memberFamilyId = await _db.FamilyMembers
                .Where(m => m.Id == request.MemberId && m.FamilyId == current!.Family.Id)
                .Select(m => (Guid?)m.FamilyId)
                .FirstOrDefaultAsync(cancellationToken);
            if (memberFamilyId is null)
            {
                return ApplicationError.Validation("agenda.unknown_member", "Member does not belong to this family.");
            }

            var entity = new MemberAgendaPattern
            {
                FamilyId = memberFamilyId.Value,
                FamilyMemberId = request.MemberId,
                DayOfWeek = request.DayOfWeek,
                Label = request.Label.Trim(),
                Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                TransportMode = request.TransportMode,
                IsAway = request.IsAway,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                IsActive = request.IsActive,
            };

            _db.MemberAgendaPatterns.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return new MemberAgendaPatternDto(
                entity.Id, entity.FamilyMemberId, entity.DayOfWeek, entity.Label, entity.Location,
                entity.StartTime, entity.EndTime, entity.TransportMode, entity.IsAway, entity.Notes, entity.IsActive);
        }
    }
}
