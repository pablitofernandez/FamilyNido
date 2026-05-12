namespace FamilyNido.Api.Features.Scores;

/// <summary>One row of the family scoreboard for a date range.</summary>
/// <param name="MemberId">Owning member.</param>
/// <param name="Points">Sum of <c>HouseholdTask.Points</c> across the member's completions in the range.</param>
/// <param name="CompletionCount">How many occurrences the member ticked in the range.</param>
public sealed record ScoreboardEntryDto(Guid MemberId, int Points, int CompletionCount);

/// <summary>Bundle returned by <c>GET /api/scores/leaderboard</c>.</summary>
/// <param name="From">Inclusive lower bound of the range.</param>
/// <param name="To">Inclusive upper bound of the range.</param>
/// <param name="Entries">Members ordered by points descending, ties broken by completion count.</param>
public sealed record LeaderboardDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<ScoreboardEntryDto> Entries);

/// <summary>Per-member totals returned by <c>GET /api/scores/members/{memberId}</c>.</summary>
/// <param name="MemberId">Owning member.</param>
/// <param name="ThisWeek">Sum of points for the current ISO week (Monday→Sunday in local time).</param>
/// <param name="ThisMonth">Sum of points for the current calendar month.</param>
/// <param name="AllTime">Sum of points across the member's whole history.</param>
public sealed record MemberScoreDto(
    Guid MemberId,
    int ThisWeek,
    int ThisMonth,
    int AllTime);

/// <summary>One row in the per-member completion history.</summary>
/// <param name="TaskId">Owning task — null when the task has been hard-deleted.</param>
/// <param name="TaskTitle">Title of the task at the time the row is read (or "(eliminada)").</param>
/// <param name="OccurrenceDate">Date of the occurrence the member ticked.</param>
/// <param name="CompletedAt">UTC instant the row was created.</param>
/// <param name="Points">Reward earned (read live from <c>HouseholdTask.Points</c>; 0 when the task is gone).</param>
public sealed record MemberCompletionDto(
    Guid? TaskId,
    string TaskTitle,
    DateOnly OccurrenceDate,
    DateTimeOffset CompletedAt,
    int Points);
