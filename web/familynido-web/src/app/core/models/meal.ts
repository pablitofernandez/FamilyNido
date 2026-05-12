/** Slot of the day mirrored from the backend `MealSlot` enum. */
export type MealSlot = 'Lunch' | 'Dinner';

/** Course within a meal slot mirrored from the backend `MealCourse` enum. */
export type MealCourse = 'First' | 'Second';

/** Single meal-plan slot row — both courses are optional, but at least one is non-empty. */
export interface MealPlanSlotEntry {
  id: string;
  date: string; // YYYY-MM-DD
  slot: MealSlot;
  firstCourse: string | null;
  secondCourse: string | null;
}

/** Day in the week-grid: each slot is null when the family has not planned anything. */
export interface MealDay {
  date: string;
  lunch: MealPlanSlotEntry | null;
  dinner: MealPlanSlotEntry | null;
}

/** Response of `GET /api/meals/week`. */
export interface MealWeek {
  weekStart: string;
  days: MealDay[];
}

/** Body for `PUT /api/meals/slots`. */
export interface UpsertMealSlotRequest {
  date: string;
  slot: MealSlot;
  course: MealCourse;
  name: string;
}

/** Body for `POST /api/meals/week/duplicate-previous`. */
export interface DuplicatePreviousWeekRequest {
  weekStart: string;
  overwrite: boolean;
}
