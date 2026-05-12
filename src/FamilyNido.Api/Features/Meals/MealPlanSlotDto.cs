using FamilyNido.Domain.Meals;

namespace FamilyNido.Api.Features.Meals;

/// <summary>Wire-level view of a single meal-plan slot row.</summary>
/// <param name="Id">Slot row id (handy for client-side tracking).</param>
/// <param name="Date">Date of the slot (YYYY-MM-DD).</param>
/// <param name="Slot">Which meal of the day (<see cref="MealSlot"/>).</param>
/// <param name="FirstCourse">Primer plato; null when not registered.</param>
/// <param name="SecondCourse">Segundo plato; null when not registered.</param>
public sealed record MealPlanSlotDto(
    Guid Id,
    DateOnly Date,
    MealSlot Slot,
    string? FirstCourse,
    string? SecondCourse)
{
    /// <summary>Project a domain row to its DTO shape.</summary>
    public static MealPlanSlotDto From(MealPlanSlot slot)
        => new(slot.Id, slot.Date, slot.Slot, slot.FirstCourse, slot.SecondCourse);
}

/// <summary>One day in the weekly plan grid: slots are nullable to signal "empty".</summary>
/// <param name="Date">Date of the day.</param>
/// <param name="Lunch">Lunch slot for this day, or null if no row exists.</param>
/// <param name="Dinner">Dinner slot for this day, or null if no row exists.</param>
public sealed record MealDayDto(DateOnly Date, MealPlanSlotDto? Lunch, MealPlanSlotDto? Dinner);

/// <summary>Full week response: 7 days from <paramref name="WeekStart"/> (a Monday).</summary>
/// <param name="WeekStart">ISO Monday of the week the response covers.</param>
/// <param name="Days">Seven days, in order from Monday to Sunday.</param>
public sealed record MealWeekDto(DateOnly WeekStart, IReadOnlyList<MealDayDto> Days);
