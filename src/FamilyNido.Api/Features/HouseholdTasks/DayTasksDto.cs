namespace FamilyNido.Api.Features.HouseholdTasks;

/// <summary>
/// A calendar day bundled with the tasks scheduled on it. Returned by
/// <see cref="GetTodayTasks"/> and <see cref="GetWeekTasks"/>.
/// </summary>
/// <param name="Date">The date this bucket corresponds to.</param>
/// <param name="Tasks">Tasks scheduled on <paramref name="Date"/> with their occurrence state.</param>
public sealed record DayTasksDto(DateOnly Date, IReadOnlyList<TaskOnDateDto> Tasks);

/// <summary>Pair of task + its occurrence-state on a specific date.</summary>
/// <param name="Task">The task.</param>
/// <param name="Occurrence">The occurrence for the bundle date (may be uncompleted).</param>
public sealed record TaskOnDateDto(HouseholdTaskDto Task, TaskOccurrenceDto Occurrence);
