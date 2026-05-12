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

/// <summary>Slice: create a new <see cref="HouseholdTask"/> under the caller's family (RF-TASK-001).</summary>
public static class CreateHouseholdTask
{
    /// <summary>Command carrying the input payload.</summary>
    /// <param name="Title">Short title. 1-120 chars.</param>
    /// <param name="Description">Optional longer description.</param>
    /// <param name="Category">Free-form category label (defaults to "General").</param>
    /// <param name="Recurrence">Recurrence mode.</param>
    /// <param name="WeeklyDays">Selected weekdays when <paramref name="Recurrence"/> is Weekly.</param>
    /// <param name="MonthlyDay">Day of month (1..31 or -1) when <paramref name="Recurrence"/> is Monthly.</param>
    /// <param name="TimeOfDay">Optional informative time-of-day target.</param>
    /// <param name="StartDate">Pivot date (defaults to today if null at call site).</param>
    /// <param name="DueDate">Target date for single-shot tasks.</param>
    /// <param name="ResponsibleMemberId">The single member that will execute the task. Null leaves it open for anyone.</param>
    /// <param name="RelatedMemberIds">Members the task concerns (the "about-whom" of the chore).</param>
    /// <param name="IsFloating">True for "do me whenever" tasks: pending in Hoy every day until completed once.</param>
    /// <param name="Points">Reward (1..10) earned by whoever marks an occurrence done.</param>
    public sealed record Command(
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

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MaximumLength(120);

            RuleFor(x => x.Description)
                .MaximumLength(2000);

            RuleFor(x => x.Category)
                .MaximumLength(40);

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

            // Floating tasks have no schedule. Allowing recurrence or DueDate
            // alongside IsFloating would produce ambiguous semantics.
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

    /// <summary>Handler — persists a new task row linked to the caller as creator.</summary>
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

            // Resolve and validate the responsible member (if any) — must belong to
            // the same family. We do this in a single query that also covers the
            // related members so the round-trip count stays at one.
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

            // Related members exclude the responsible — same person on both sides
            // would be a meaningless duplicate.
            var relatedIds = (request.RelatedMemberIds ?? [])
                .Where(id => id != request.ResponsibleMemberId)
                .ToHashSet();
            var related = foundMembers.Where(m => relatedIds.Contains(m.Id)).ToList();

            var task = new HouseholdTask
            {
                FamilyId = current.Family.Id,
                Title = request.Title,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description,
                Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category,
                Recurrence = request.IsFloating ? RecurrenceMode.None : request.Recurrence,
                WeeklyDays = request.Recurrence == RecurrenceMode.Weekly && !request.IsFloating ? request.WeeklyDays : null,
                MonthlyDay = request.Recurrence == RecurrenceMode.Monthly && !request.IsFloating ? request.MonthlyDay : null,
                TimeOfDay = request.TimeOfDay,
                StartDate = request.StartDate,
                DueDate = request.IsFloating || request.Recurrence != RecurrenceMode.None ? null : request.DueDate,
                CreatedByMemberId = current.Member.Id,
                ResponsibleMemberId = request.ResponsibleMemberId,
                RelatedMembers = related,
                IsFloating = request.IsFloating,
                Points = request.Points,
            };

            _db.HouseholdTasks.Add(task);
            await _db.SaveChangesAsync(cancellationToken);

            if (request.ResponsibleMemberId is { } assignedId)
            {
                await _notifications.NotifyTaskAssignedAsync(assignedId, current.Member.Id, task, cancellationToken);
            }

            return HouseholdTaskDto.From(task);
        }
    }
}
