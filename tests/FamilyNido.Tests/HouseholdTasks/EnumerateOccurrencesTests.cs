using FamilyNido.Domain.HouseholdTasks;
using FluentAssertions;

namespace FamilyNido.Tests.HouseholdTasks;

/// <summary>
/// Pure-domain tests for <see cref="HouseholdTask.EnumerateOccurrences"/> — the engine that
/// drives "tasks for today / this week" queries. Kept in-process (no DB) so edge cases are
/// exhaustively covered without migration overhead.
/// </summary>
public sealed class EnumerateOccurrencesTests
{
    private static HouseholdTask MakeTask(
        RecurrenceMode mode,
        DateOnly startDate,
        DateOnly? dueDate = null,
        DayOfWeekMask? weeklyDays = null,
        int? monthlyDay = null,
        bool archived = false) => new()
        {
            FamilyId = Guid.CreateVersion7(),
            CreatedByMemberId = Guid.CreateVersion7(),
            Title = "Test",
            Recurrence = mode,
            StartDate = startDate,
            DueDate = dueDate,
            WeeklyDays = weeklyDays,
            MonthlyDay = monthlyDay,
            IsArchived = archived,
        };

    [Fact]
    public void Archived_task_yields_no_occurrences()
    {
        var task = MakeTask(RecurrenceMode.Daily, new DateOnly(2026, 1, 1), archived: true);

        task.EnumerateOccurrences(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10))
            .Should().BeEmpty();
    }

    [Fact]
    public void Inverted_range_yields_no_occurrences()
    {
        var task = MakeTask(RecurrenceMode.Daily, new DateOnly(2026, 1, 1));

        task.EnumerateOccurrences(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 1))
            .Should().BeEmpty();
    }

    [Fact]
    public void Daily_yields_one_occurrence_per_day_in_range()
    {
        var task = MakeTask(RecurrenceMode.Daily, new DateOnly(2026, 1, 1));

        var occurrences = task.EnumerateOccurrences(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 8)).ToList();

        occurrences.Should().Equal(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 6),
            new DateOnly(2026, 1, 7),
            new DateOnly(2026, 1, 8));
    }

    [Fact]
    public void Daily_respects_start_date_as_floor()
    {
        var task = MakeTask(RecurrenceMode.Daily, new DateOnly(2026, 1, 10));

        var occurrences = task.EnumerateOccurrences(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 12)).ToList();

        occurrences.Should().Equal(
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 11),
            new DateOnly(2026, 1, 12));
    }

    [Fact]
    public void Weekly_only_yields_selected_weekdays()
    {
        // 2026-04-20 is Monday; 2026-05-03 is Sunday.
        var task = MakeTask(
            RecurrenceMode.Weekly,
            new DateOnly(2026, 4, 20),
            weeklyDays: DayOfWeekMask.Monday | DayOfWeekMask.Thursday);

        var occurrences = task.EnumerateOccurrences(
            new DateOnly(2026, 4, 20),
            new DateOnly(2026, 5, 3)).ToList();

        occurrences.Should().Equal(
            new DateOnly(2026, 4, 20), // Mon
            new DateOnly(2026, 4, 23), // Thu
            new DateOnly(2026, 4, 27), // Mon
            new DateOnly(2026, 4, 30)); // Thu — May 3 is Sunday, not selected.
    }

    [Fact]
    public void Weekly_with_no_selected_days_yields_nothing()
    {
        var task = MakeTask(
            RecurrenceMode.Weekly,
            new DateOnly(2026, 4, 20),
            weeklyDays: DayOfWeekMask.None);

        task.EnumerateOccurrences(new DateOnly(2026, 4, 20), new DateOnly(2026, 5, 20))
            .Should().BeEmpty();
    }

    [Fact]
    public void Weekly_null_selection_yields_nothing()
    {
        var task = MakeTask(RecurrenceMode.Weekly, new DateOnly(2026, 4, 20));

        task.EnumerateOccurrences(new DateOnly(2026, 4, 20), new DateOnly(2026, 5, 20))
            .Should().BeEmpty();
    }

    [Fact]
    public void Monthly_yields_one_occurrence_per_month_on_matching_day()
    {
        var task = MakeTask(
            RecurrenceMode.Monthly,
            new DateOnly(2026, 1, 1),
            monthlyDay: 15);

        var occurrences = task.EnumerateOccurrences(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 4, 30)).ToList();

        occurrences.Should().Equal(
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 2, 15),
            new DateOnly(2026, 3, 15),
            new DateOnly(2026, 4, 15));
    }

    [Fact]
    public void Monthly_day_31_collapses_to_last_day_of_short_months()
    {
        var task = MakeTask(
            RecurrenceMode.Monthly,
            new DateOnly(2026, 1, 1),
            monthlyDay: 31);

        var occurrences = task.EnumerateOccurrences(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 4, 30)).ToList();

        occurrences.Should().Equal(
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 2, 28), // 2026 is not a leap year
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 30));
    }

    [Fact]
    public void Monthly_minus_one_always_yields_last_day_of_month()
    {
        var task = MakeTask(
            RecurrenceMode.Monthly,
            new DateOnly(2024, 1, 1),
            monthlyDay: -1);

        var occurrences = task.EnumerateOccurrences(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 4, 30)).ToList();

        occurrences.Should().Equal(
            new DateOnly(2024, 1, 31),
            new DateOnly(2024, 2, 29), // 2024 is a leap year
            new DateOnly(2024, 3, 31),
            new DateOnly(2024, 4, 30));
    }

    [Fact]
    public void Monthly_null_day_yields_nothing()
    {
        var task = MakeTask(RecurrenceMode.Monthly, new DateOnly(2026, 1, 1));

        task.EnumerateOccurrences(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))
            .Should().BeEmpty();
    }

    [Fact]
    public void None_with_same_start_and_due_date_yields_single_occurrence()
    {
        // Single-shot task: StartDate == DueDate. Behaves as a one-day occurrence.
        var task = MakeTask(
            RecurrenceMode.None,
            new DateOnly(2026, 4, 25),
            dueDate: new DateOnly(2026, 4, 25));

        task.EnumerateOccurrences(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 30))
            .Should().Equal(new DateOnly(2026, 4, 25));
    }

    [Fact]
    public void None_deadline_range_yields_every_day_in_window_intersection()
    {
        // Deadline task: StartDate < DueDate. Surfaces every day in
        // [StartDate, DueDate] clipped to the requested window so the family
        // sees it every morning until they finally complete it.
        var task = MakeTask(
            RecurrenceMode.None,
            new DateOnly(2026, 1, 1),
            dueDate: new DateOnly(2026, 4, 25));

        task.EnumerateOccurrences(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 30))
            .Should().Equal(
                new DateOnly(2026, 4, 20),
                new DateOnly(2026, 4, 21),
                new DateOnly(2026, 4, 22),
                new DateOnly(2026, 4, 23),
                new DateOnly(2026, 4, 24),
                new DateOnly(2026, 4, 25));
    }

    [Fact]
    public void None_deadline_range_clips_to_window_when_due_falls_after()
    {
        // DueDate after the window — yield every day inside the window.
        var task = MakeTask(
            RecurrenceMode.None,
            new DateOnly(2026, 1, 1),
            dueDate: new DateOnly(2026, 5, 10));

        task.EnumerateOccurrences(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 22))
            .Should().Equal(
                new DateOnly(2026, 4, 20),
                new DateOnly(2026, 4, 21),
                new DateOnly(2026, 4, 22));
    }

    [Fact]
    public void None_without_due_date_falls_back_to_start_date()
    {
        var task = MakeTask(RecurrenceMode.None, new DateOnly(2026, 4, 25));

        task.EnumerateOccurrences(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 30))
            .Should().Equal(new DateOnly(2026, 4, 25));
    }

    [Fact]
    public void Has_occurrence_on_matches_weekly_day()
    {
        // 2026-04-25 is Saturday.
        var task = MakeTask(
            RecurrenceMode.Weekly,
            new DateOnly(2026, 4, 1),
            weeklyDays: DayOfWeekMask.Saturday);

        task.HasOccurrenceOn(new DateOnly(2026, 4, 25)).Should().BeTrue();
        task.HasOccurrenceOn(new DateOnly(2026, 4, 24)).Should().BeFalse();
    }

    [Fact]
    public void Has_occurrence_on_respects_start_date()
    {
        var task = MakeTask(RecurrenceMode.Daily, new DateOnly(2026, 4, 25));

        task.HasOccurrenceOn(new DateOnly(2026, 4, 24)).Should().BeFalse();
        task.HasOccurrenceOn(new DateOnly(2026, 4, 25)).Should().BeTrue();
    }
}
