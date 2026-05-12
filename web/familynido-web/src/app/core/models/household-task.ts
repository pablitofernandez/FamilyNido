/**
 * Recurrence mode mirrors {@link FamilyNido.Domain.HouseholdTasks.RecurrenceMode}.
 * Kept as a string union so the serialized value matches the API (backend ships
 * `JsonStringEnumConverter` globally).
 */
export type RecurrenceMode = 'None' | 'Daily' | 'Weekly' | 'Monthly';

/**
 * Bitmask flag union for weekdays. Flags-enum JSON in System.Text.Json serializes
 * as a comma-separated string (e.g. `"Monday, Thursday"`); the Angular side keeps
 * the string as-is and splits on the UI side when rendering day chips.
 */
export type DayOfWeekMask =
  | 'None'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'
  | 'Sunday'
  | 'Weekdays'
  | 'Weekend'
  | 'All'
  | string; // combinations come as "Monday, Thursday"

/** Shape returned by `GET /api/household-tasks` (single item). */
export interface HouseholdTask {
  id: string;
  title: string;
  description: string | null;
  category: string;
  recurrence: RecurrenceMode;
  weeklyDays: DayOfWeekMask | null;
  monthlyDay: number | null;
  timeOfDay: string | null; // "HH:mm:ss"
  startDate: string; // "YYYY-MM-DD"
  dueDate: string | null;
  /** The single member that executes the task. Null = open, anyone can do it. */
  responsibleMemberId: string | null;
  /** Members the task concerns ("about whom"). */
  relatedMemberIds: string[];
  isArchived: boolean;
  /** "Do me whenever" task — pending in Hoy every day until first completion. */
  isFloating: boolean;
  createdByMemberId: string;
  createdAt: string;
  /** Reward (1..10) earned by whoever marks an occurrence done. */
  points: number;
  /** Most recent completion of this task, or null if it has never been completed. */
  latestCompletion: LatestCompletion | null;
}

/** Compact view of the most recent completion of a task — used by the "Todas" tab. */
export interface LatestCompletion {
  /** YYYY-MM-DD */
  occurrenceDate: string;
  completedByMemberId: string | null;
  /** ISO instant. */
  completedAt: string;
}

/** Single occurrence (date + completion state) of a task. */
export interface TaskOccurrence {
  taskId: string;
  occurrenceDate: string;
  isCompleted: boolean;
  completedByMemberId: string | null;
  completedAt: string | null;
  note: string | null;
}

/** A day bundled with all tasks scheduled on it. */
export interface DayTasks {
  date: string;
  tasks: { task: HouseholdTask; occurrence: TaskOccurrence }[];
}

/** Payload for `POST /api/household-tasks`. */
export interface CreateHouseholdTaskRequest {
  title: string;
  description?: string | null;
  category?: string | null;
  recurrence: RecurrenceMode;
  weeklyDays?: DayOfWeekMask | null;
  monthlyDay?: number | null;
  timeOfDay?: string | null;
  startDate: string;
  dueDate?: string | null;
  responsibleMemberId?: string | null;
  relatedMemberIds?: string[] | null;
  isFloating: boolean;
  /** Reward (1..10). Defaults server-side to 5 when omitted is not allowed. */
  points: number;
}

/** Payload for `PATCH /api/household-tasks/{id}`. */
export type UpdateHouseholdTaskRequest = CreateHouseholdTaskRequest;

/** Payload for completing an occurrence. */
export interface CompleteOccurrenceRequest {
  note?: string | null;
}

/** Payload for the admin attribution upsert (PUT .../completion). */
export interface SetCompletionAttributionRequest {
  completedByMemberId: string;
  note?: string | null;
}

/** One entry in the per-task completion history (GET .../completions). */
export interface TaskCompletionEntry {
  /** YYYY-MM-DD */
  occurrenceDate: string;
  completedByMemberId: string | null;
  /** ISO instant. */
  completedAt: string;
  note: string | null;
}
