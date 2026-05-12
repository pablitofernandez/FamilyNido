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
/// Slice for <c>PUT /api/member-agenda/patterns/{id}</c>. Replaces the row in
/// place. Admin can edit any pattern; non-admins only their own.
/// </summary>
public static class UpdateMemberAgendaPattern
{
    /// <summary>Command carrying the new field values.</summary>
    public sealed record Command(
        Guid Id,
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

    /// <summary>Mutates the row.</summary>
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
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var entity = await _db.MemberAgendaPatterns
                .FirstOrDefaultAsync(p => p.Id == request.Id && p.FamilyId == current.Family.Id, cancellationToken);
            if (entity is null)
            {
                return ApplicationError.NotFound("agenda.pattern_not_found", "Agenda pattern not found.");
            }
            if (!MemberAgendaPermissions.CanWrite(current, entity.FamilyMemberId))
            {
                return ApplicationError.Forbidden("agenda.forbidden", "Only an admin or the member themselves can edit this agenda.");
            }

            entity.DayOfWeek = request.DayOfWeek;
            entity.Label = request.Label.Trim();
            entity.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
            entity.StartTime = request.StartTime;
            entity.EndTime = request.EndTime;
            entity.TransportMode = request.TransportMode;
            entity.IsAway = request.IsAway;
            entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            entity.IsActive = request.IsActive;

            await _db.SaveChangesAsync(cancellationToken);

            return new MemberAgendaPatternDto(
                entity.Id, entity.FamilyMemberId, entity.DayOfWeek, entity.Label, entity.Location,
                entity.StartTime, entity.EndTime, entity.TransportMode, entity.IsAway, entity.Notes, entity.IsActive);
        }
    }
}
