using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Features.Notifications;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>Slice: update an existing task. Mutates all editable fields in one shot (RF-TASK-004).</summary>
public static class UpdateHouseholdTask
{
    /// <summary>Command carrying the target id + mutable payload.</summary>
    /// <param name="TaskId">Task to update.</param>
    /// <param name="Title">New title.</param>
    /// <param name="Description">New description (null clears).</param>
    /// <param name="Category">Category label.</param>
    /// <param name="Recurrence">Recurrence mode.</param>
    /// <param name="WeeklyDays">Weekday mask when Weekly.</param>
    /// <param name="MonthlyDay">Day-of-month when Monthly.</param>
    /// <param name="TimeOfDay">Informative time-of-day.</param>
    /// <param name="StartDate">Pivot date.</param>
    /// <param name="DueDate">Target date for single-shot tasks.</param>
    /// <param name="ResponsibleMemberId">The single member who executes the task. Null leaves it open.</param>
    /// <param name="RelatedMemberIds">Members the task concerns.</param>
    /// <param name="IsFloating">True for "do me whenever" tasks: pending in Hoy until completed once.</param>
    /// <param name="Points">Reward (1..10) earned by whoever marks an occurrence done.</param>
    public sealed record Command(
        Guid TaskId,
        string Title,
        string? Description,
        string? Category,
        RecurrenceMode Recurrence,
        DayOfWeekMask? WeeklyDays,
        int? MonthlyDay,
        TimeOnly? TimeOfDay,
        DateOnly StartDate,
        DateOnly? DueDate,
        Guid? ResponsibleMemberId,
        IReadOnlyList<Guid>? RelatedMemberIds,
        bool IsFloating,
        int Points) : IRequest<Result<HouseholdTaskDto>>;

    /// <summary>Input validation — shares the rules from <see cref="CreateHouseholdTask"/>.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.TaskId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Category).MaximumLength(40);

            RuleFor(x => x.WeeklyDays)
                .NotNull()
                .NotEqual(DayOfWeekMask.None)
                .When(x => x.Recurrence == RecurrenceMode.Weekly)
                .WithMessage("Weekly tasks must select at least one weekday.");

            RuleFor(x => x.MonthlyDay)
                .NotNull()
                .Must(d => d is -1 || (d >= 1 && d <= 31))
                .When(x => x.Recurrence == RecurrenceMode.Monthly)
                .WithMessage("MonthlyDay must be 1..31 or -1 (last day of month).");

            RuleFor(x => x.DueDate)
                .Null()
                .When(x => x.Recurrence != RecurrenceMode.None)
                .WithMessage("DueDate is only valid for non-recurring tasks.");

            RuleFor(x => x.Recurrence)
                .Equal(RecurrenceMode.None)
                .When(x => x.IsFloating)
                .WithMessage("Floating tasks must use recurrence None.");
            RuleFor(x => x.DueDate)
                .Null()
                .When(x => x.IsFloating)
                .WithMessage("Floating tasks cannot have a target date.");

            RuleFor(x => x.Points)
                .InclusiveBetween(1, 10)
                .WithMessage("Points must be between 1 and 10.");
        }
    }

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<HouseholdTaskDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly NotificationService _notifications;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            NotificationService notifications)
        {
            _db = db;
            _userContext = userContext;
            _notifications = notifications;
        }

        /// <inheritdoc />
        public async Task<Result<HouseholdTaskDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var task = await _db.HouseholdTasks
                .Include(t => t.RelatedMembers)
                .FirstOrDefaultAsync(
                    t => t.Id == request.TaskId && t.FamilyId == current.Family.Id,
                    cancellationToken);

            if (task is null)
            {
                return ApplicationError.NotFound("household_task.not_found", $"Task {request.TaskId} not found.");
            }

            // Validate all referenced members in a single round-trip — responsible
            // and related ids combined.
            var memberIdsToValidate = new HashSet<Guid>(request.RelatedMemberIds ?? []);
            if (request.ResponsibleMemberId is { } responsibleId)
            {
                memberIdsToValidate.Add(responsibleId);
            }

            var foundMembers = memberIdsToValidate.Count == 0
                ? []
                : await _db.FamilyMembers
                    .Where(m => m.FamilyId == current.Family.Id && memberIdsToValidate.Contains(m.Id))
                    .ToListAsync(cancellationToken);

            if (foundMembers.Count != memberIdsToValidate.Count)
            {
                return ApplicationError.Validation(
                    "household_task.unknown_member",
                    "One or more referenced members are not part of this family.");
            }

            task.Title = request.Title;
            task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description;
            task.Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category;
            task.IsFloating = request.IsFloating;
            task.Recurrence = request.IsFloating ? RecurrenceMode.None : request.Recurrence;
            task.WeeklyDays = !request.IsFloating && request.Recurrence == RecurrenceMode.Weekly ? request.WeeklyDays : null;
            task.MonthlyDay = !request.IsFloating && request.Recurrence == RecurrenceMode.Monthly ? request.MonthlyDay : null;
            task.TimeOfDay = request.TimeOfDay;
            task.StartDate = request.StartDate;
            task.DueDate = request.IsFloating || request.Recurrence != RecurrenceMode.None ? null : request.DueDate;
            task.Points = request.Points;
            // Capture the previous responsible so we only fire on actual changes.
            var previousResponsibleId = task.ResponsibleMemberId;
            task.ResponsibleMemberId = request.ResponsibleMemberId;

            // Replace the related set; the responsible never doubles as related.
            task.RelatedMembers.Clear();
            var relatedIds = (request.RelatedMemberIds ?? [])
                .Where(id => id != request.ResponsibleMemberId)
                .ToHashSet();
            foreach (var related in foundMembers.Where(m => relatedIds.Contains(m.Id)))
            {
                task.RelatedMembers.Add(related);
            }

            await _db.SaveChangesAsync(cancellationToken);

            if (task.ResponsibleMemberId is { } assignedId && assignedId != previousResponsibleId)
            {
                await _notifications.NotifyTaskAssignedAsync(assignedId, current.Member.Id, task, cancellationToken);
            }

            return HouseholdTaskDto.From(task);
        }
    }
}
