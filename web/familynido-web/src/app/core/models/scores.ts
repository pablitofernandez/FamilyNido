/** One row of the family scoreboard for a date range. */
export interface ScoreboardEntry {
  memberId: string;
  points: number;
  completionCount: number;
}

/** Wire shape returned by `GET /api/scores/leaderboard`. */
export interface Leaderboard {
  from: string;
  to: string;
  entries: ScoreboardEntry[];
}

/** Per-member totals returned by `GET /api/scores/members/{id}`. */
export interface MemberScore {
  memberId: string;
  thisWeek: number;
  thisMonth: number;
  allTime: number;
}

/** One row in `GET /api/scores/members/{id}/history`. */
export interface MemberCompletion {
  taskId: string | null;
  taskTitle: string;
  /** YYYY-MM-DD */
  occurrenceDate: string;
  /** ISO instant */
  completedAt: string;
  points: number;
}
