/** Stable widget identifiers — must match the backend catalogue. */
export type DashboardWidgetId =
  | 'weather'
  | 'school'
  | 'agenda'
  | 'tasks'
  | 'calendar'
  | 'meals'
  | 'wall'
  | 'scores'
  | 'birthdays';

/** One entry in the user's ordered widget list. */
export interface DashboardWidget {
  id: DashboardWidgetId;
  visible: boolean;
}

/** Wire shape returned by GET/PUT `/api/dashboard/preferences`. */
export interface DashboardPreferences {
  widgets: DashboardWidget[];
}
