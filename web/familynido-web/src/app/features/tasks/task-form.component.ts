import { NgClass } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  LOCALE_ID,
  OnChanges,
  SimpleChanges,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { HouseholdTasksService } from '../../core/api/household-tasks.service';
import { FamilyMember } from '../../core/models/family-member';
import {
  CreateHouseholdTaskRequest,
  DayOfWeekMask,
  HouseholdTask,
  RecurrenceMode,
  TaskCompletionEntry,
} from '../../core/models/household-task';

/** What the form emits on save. `id` null ⇒ create; otherwise update. */
export interface TaskFormResult {
  id: string | null;
  payload: CreateHouseholdTaskRequest;
  /**
   * Completion delta computed by the form against the task's
   * `latestCompletion` at the time it was opened. `null` when nothing
   * changed (or when the task is being created — no completion exists
   * yet to compare against). Only emitted for admins; non-admins never
   * see the completion section so the diff is always null.
   */
  completionOps: CompletionOps | null;
}

/**
 * Granular instruction for the parent component to apply on top of the
 * regular task save. The two slots are independent:
 *  - `toRemove`: the date of an existing completion that should be undone
 *    (the task was completed before, the admin toggled it off, or the
 *    admin moved the completion to a different date).
 *  - `toAdd`: the (date, memberId) pair to upsert via the admin endpoint
 *    (the task is now completed, possibly at the same or a new date).
 *
 * "Same date, same member" produces a no-op result with both slots null.
 * "Same date, different member" populates only `toAdd` — the PUT endpoint
 * updates the attribution in place. "Different date" populates both.
 */
export interface CompletionOps {
  toRemove: string | null;
  toAdd: { date: string; memberId: string } | null;
}

/** Ordered day buttons shown for Weekly tasks. */
const WEEKDAYS: { label: string; flag: string }[] = [
  { label: 'L', flag: 'Monday' },
  { label: 'M', flag: 'Tuesday' },
  { label: 'X', flag: 'Wednesday' },
  { label: 'J', flag: 'Thursday' },
  { label: 'V', flag: 'Friday' },
  { label: 'S', flag: 'Saturday' },
  { label: 'D', flag: 'Sunday' },
];

/** Inline editor panel for creating/editing a HouseholdTask. */
@Component({
  selector: 'fn-task-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgClass],
  templateUrl: './task-form.component.html',
  styleUrl: './task-form.component.css',
})
export class TaskFormComponent implements OnChanges {
  private readonly api = inject(HouseholdTasksService);
  private readonly locale = inject(LOCALE_ID);

  readonly members = input<FamilyMember[]>([]);
  readonly initial = input<HouseholdTask | null>(null);
  /** Surfaces the admin-only "Completada / quién / cuándo" section. */
  readonly isAdmin = input<boolean>(false);
  readonly save = output<TaskFormResult>();
  readonly cancel = output<void>();

  protected readonly weekdays = WEEKDAYS;
  protected readonly recurrenceOpts: { value: RecurrenceMode; label: string }[] = [
    { value: 'None', label: $localize`:@@task-form.recurrence.once:Única` },
    { value: 'Daily', label: $localize`:@@task-form.recurrence.daily:Diaria` },
    { value: 'Weekly', label: $localize`:@@task-form.recurrence.weekly:Semanal` },
    { value: 'Monthly', label: $localize`:@@task-form.recurrence.monthly:Mensual` },
  ];

  /** Aria-label per reward segment ("Recompensa 7"). */
  protected rewardSegmentAriaLabel(n: number): string {
    return $localize`:@@task-form.reward-segment-aria:Recompensa ${n}:N:`;
  }

  protected readonly title = signal('');
  protected description = '';
  protected category = 'General';
  protected timeOfDay = '';
  protected readonly recurrence = signal<RecurrenceMode>('None');
  protected readonly selectedDays = signal<string[]>([]);
  protected monthlyDay: number | null = 1;
  protected lastDayOfMonth = false;
  protected startDate = this.todayIso();
  protected dueDate = '';
  /** Singular: who *executes* the task. Empty string ⇒ open. */
  protected readonly responsibleId = signal<string>('');
  /** N:M: members the task *concerns*. */
  protected readonly selectedRelated = signal<string[]>([]);
  /** Floating: no fixed date, sticks in Hoy until completed once. */
  protected readonly isFloating = signal<boolean>(false);
  /** Reward 1..10 — drives the family scoreboard. Default mid-range. */
  protected readonly points = signal<number>(5);

  /** 1..10 array used to render the segmented reward bar. */
  protected readonly pointSegments = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  // ─── completion edit (admin only) ─────────────────────────────────────────
  /** Toggle: should the task be marked as completed? */
  protected readonly completionEnabled = signal<boolean>(false);
  /** OccurrenceDate of the completion (YYYY-MM-DD). */
  protected readonly completionDate = signal<string>('');
  /** Member id credited as the completer. */
  protected readonly completionMemberId = signal<string>('');
  /**
   * Snapshot of the task's `latestCompletion` at hydrate time. Used to diff
   * the form state on submit so we know whether to undo, upsert, or both.
   * `null` when the task wasn't completed (or this is a new task).
   */
  private originalCompletion: { date: string; memberId: string | null } | null = null;

  // ─── completion history (admin only, edit mode only) ─────────────────────
  /** All completions for the current task, most recent first. Lazy-loaded on hydrate. */
  protected readonly completions = signal<TaskCompletionEntry[]>([]);
  /** True while the GET .../completions request is in flight. */
  protected readonly loadingHistory = signal<boolean>(false);
  /** OccurrenceDate of the row currently being re-attributed; null = nothing open. */
  protected readonly editingHistoryDate = signal<string | null>(null);
  /** True while a per-row save (PUT or undo) is in flight. */
  protected readonly savingHistoryRow = signal<boolean>(false);

  /** "+ Añadir completado" form open in the Historial panel. */
  protected readonly addingHistoryEntry = signal<boolean>(false);
  /** Date for the new historial entry (YYYY-MM-DD). Defaults to today. */
  protected readonly newEntryDate = signal<string>('');
  /** Member id credited as completer of the new entry. */
  protected readonly newEntryMemberId = signal<string>('');
  /** Last validation/error message returned by the API for the add form. */
  protected readonly addEntryError = signal<string | null>(null);

  /**
   * Enabled once the user types anything non-blank into the title field. If
   * the admin has the completion toggle on, both the date and the member
   * must also be set — otherwise the task save would silently skip the
   * completion side.
   */
  protected canSave(): boolean {
    if (this.title().trim().length === 0) {
      return false;
    }
    if (this.isAdmin() && this.completionEnabled()) {
      if (!this.completionDate() || !this.completionMemberId()) {
        return false;
      }
    }
    return true;
  }

  /** Read the current value of the HTMLInputElement that triggered an input event. */
  protected readInput(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ('initial' in changes) {
      this.hydrate(this.initial());
    }
  }

  protected toggleDay(flag: string): void {
    this.selectedDays.update((days) =>
      days.includes(flag) ? days.filter((d) => d !== flag) : [...days, flag],
    );
  }

  protected toggleRelated(id: string): void {
    // The responsible never doubles as related — choosing the same person
    // again doesn't make sense, so the chip is ignored / visually disabled.
    if (id === this.responsibleId()) {
      return;
    }
    this.selectedRelated.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );
  }

  protected setResponsible(id: string): void {
    this.responsibleId.set(id);
    // If the new responsible was a related, drop them from the related set
    // so we never persist the same id on both sides.
    if (id) {
      this.selectedRelated.update((ids) => ids.filter((x) => x !== id));
    }
  }

  protected onResponsibleChange(event: Event): void {
    this.setResponsible((event.target as HTMLSelectElement).value);
  }

  protected submit(): void {
    // When the task is floating, schedule fields are forced to the safe
    // defaults expected by the backend validators (no recurrence, no due date).
    const floating = this.isFloating();
    const recurrence: RecurrenceMode = floating ? 'None' : this.recurrence();
    const payload: CreateHouseholdTaskRequest = {
      title: this.title().trim(),
      description: this.description.trim() || null,
      category: this.category.trim() || 'General',
      recurrence,
      weeklyDays:
        !floating && recurrence === 'Weekly' && this.selectedDays().length > 0
          ? (this.selectedDays().join(', ') as DayOfWeekMask)
          : null,
      monthlyDay:
        !floating && recurrence === 'Monthly'
          ? this.lastDayOfMonth
            ? -1
            : Number(this.monthlyDay ?? 1)
          : null,
      timeOfDay: this.timeOfDay ? `${this.timeOfDay}:00` : null,
      startDate: this.startDate,
      dueDate: !floating && recurrence === 'None' && this.dueDate ? this.dueDate : null,
      responsibleMemberId: this.responsibleId() || null,
      relatedMemberIds: this.selectedRelated(),
      isFloating: floating,
      points: this.points(),
    };
    this.save.emit({
      id: this.initial()?.id ?? null,
      payload,
      completionOps: this.buildCompletionOps(),
    });
  }

  /** Sets the reward bar to N (1..10). Used by the segmented bar buttons. */
  protected setPoints(value: number): void {
    if (value < 1 || value > 10) return;
    this.points.set(value);
  }

  /** Read the slider input value as a number, clamped to 1..10. */
  protected onPointsSlider(event: Event): void {
    const v = Number((event.target as HTMLInputElement).value);
    if (Number.isFinite(v)) {
      this.setPoints(Math.max(1, Math.min(10, Math.round(v))));
    }
  }

  private hydrate(task: HouseholdTask | null): void {
    if (task === null) {
      this.title.set('');
      this.description = '';
      this.category = 'General';
      this.timeOfDay = '';
      this.recurrence.set('None');
      this.selectedDays.set([]);
      this.monthlyDay = 1;
      this.lastDayOfMonth = false;
      this.startDate = this.todayIso();
      this.dueDate = '';
      this.responsibleId.set('');
      this.selectedRelated.set([]);
      this.isFloating.set(false);
      this.points.set(5);
      this.completionEnabled.set(false);
      this.completionDate.set(this.todayIso());
      this.completionMemberId.set('');
      this.originalCompletion = null;
      return;
    }
    this.title.set(task.title);
    this.description = task.description ?? '';
    this.category = task.category;
    this.timeOfDay = task.timeOfDay ? task.timeOfDay.slice(0, 5) : '';
    this.recurrence.set(task.recurrence);
    this.selectedDays.set(
      task.weeklyDays && task.weeklyDays !== 'None'
        ? task.weeklyDays.split(',').map((s) => s.trim())
        : [],
    );
    this.lastDayOfMonth = task.monthlyDay === -1;
    this.monthlyDay = task.monthlyDay === -1 ? 1 : (task.monthlyDay ?? 1);
    this.startDate = task.startDate;
    this.dueDate = task.dueDate ?? '';
    this.responsibleId.set(task.responsibleMemberId ?? '');
    this.selectedRelated.set([...task.relatedMemberIds]);
    this.isFloating.set(task.isFloating);
    this.points.set(Math.max(1, Math.min(10, task.points ?? 5)));

    // Completion section: pre-populate from latestCompletion, snapshot the
    // original so submit() can diff against it.
    const last = task.latestCompletion;
    if (last) {
      this.completionEnabled.set(true);
      this.completionDate.set(last.occurrenceDate);
      this.completionMemberId.set(last.completedByMemberId ?? '');
      this.originalCompletion = {
        date: last.occurrenceDate,
        memberId: last.completedByMemberId,
      };
    } else {
      this.completionEnabled.set(false);
      // Default the date picker to the task's due date when present, else today.
      this.completionDate.set(task.dueDate ?? this.todayIso());
      this.completionMemberId.set('');
      this.originalCompletion = null;
    }

    // Lazy-load the per-occurrence history for the Historial panel. Admin
    // gating is template-side; we still issue the request so non-admins
    // refreshing into admin within the same session see fresh data on next
    // open. The request is cheap (sorted index scan).
    this.completions.set([]);
    this.editingHistoryDate.set(null);
    this.loadingHistory.set(true);
    this.api.listCompletions(task.id).subscribe({
      next: (entries) => {
        this.completions.set(entries);
        this.loadingHistory.set(false);
      },
      error: () => this.loadingHistory.set(false),
    });
  }

  /**
   * Compute the diff between the form's completion fields and the snapshot
   * captured at hydrate time. Returns null when the form isn't admin (so we
   * never accidentally overwrite an existing completion as a non-admin) or
   * the task is being created — in both cases the parent ignores the slot.
   */
  private buildCompletionOps(): CompletionOps | null {
    if (!this.isAdmin() || this.initial() === null) {
      return null;
    }
    const orig = this.originalCompletion;
    const enabled = this.completionEnabled();

    if (!enabled) {
      // Toggled off. Either undo the existing completion or no-op.
      return orig ? { toRemove: orig.date, toAdd: null } : null;
    }

    const date = this.completionDate();
    const memberId = this.completionMemberId();
    if (!date || !memberId) {
      // Form is in an invalid state (admin enabled the toggle but didn't
      // pick member or date). Skip the completion side; the parent treats
      // this as no-op. The save button gating prevents the user from
      // reaching here with valid input missing.
      return null;
    }

    if (orig === null) {
      // Was uncompleted, now completed. Pure upsert.
      return { toRemove: null, toAdd: { date, memberId } };
    }

    if (orig.date === date && orig.memberId === memberId) {
      // No real change.
      return null;
    }

    if (orig.date === date) {
      // Same occurrence, different attributee. PUT updates in place.
      return { toRemove: null, toAdd: { date, memberId } };
    }

    // Different occurrence date — must undo old, then create new.
    return { toRemove: orig.date, toAdd: { date, memberId } };
  }

  protected setCompletionEnabled(event: Event): void {
    this.completionEnabled.set((event.target as HTMLInputElement).checked);
  }

  protected onCompletionDateChange(event: Event): void {
    this.completionDate.set((event.target as HTMLInputElement).value);
  }

  protected onCompletionMemberChange(event: Event): void {
    this.completionMemberId.set((event.target as HTMLSelectElement).value);
  }

  // ─── history per-row actions (admin) ─────────────────────────────────────

  protected isEditingHistoryRow(date: string): boolean {
    return this.editingHistoryDate() === date;
  }

  protected openHistoryRow(date: string): void {
    this.editingHistoryDate.set(date);
  }

  protected closeHistoryRow(): void {
    this.editingHistoryDate.set(null);
  }

  /** Pretty "24 abr 2026" for the history list rows. */
  protected historyRowLabel(date: string): string {
    return new Date(date + 'T00:00:00').toLocaleDateString(this.locale, {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  }

  protected memberDisplayName(memberId: string | null): string {
    if (!memberId) return '—';
    return this.members().find((m) => m.id === memberId)?.displayName ?? '—';
  }

  /**
   * Persist a re-attribution from the historial row. Emits a single PUT
   * (the upsert endpoint) and patches the local `completions` list on
   * success so the row updates without a round-trip.
   */
  protected onHistoryAttributionChange(date: string, currentMemberId: string | null, event: Event): void {
    const memberId = (event.target as HTMLSelectElement).value;
    if (!memberId || memberId === currentMemberId) {
      this.closeHistoryRow();
      return;
    }
    const taskId = this.initial()?.id;
    if (!taskId) return;

    this.savingHistoryRow.set(true);
    this.api
      .setCompletionAttribution(taskId, date, { completedByMemberId: memberId })
      .subscribe({
        next: () => {
          this.completions.update((list) =>
            list.map((c) =>
              c.occurrenceDate === date ? { ...c, completedByMemberId: memberId } : c,
            ),
          );
          this.savingHistoryRow.set(false);
          this.closeHistoryRow();
        },
        error: () => this.savingHistoryRow.set(false),
      });
  }

  // ─── add a new historial entry (admin) ───────────────────────────────────

  protected openAddHistoryEntry(): void {
    this.addingHistoryEntry.set(true);
    this.newEntryDate.set(this.todayIso());
    this.newEntryMemberId.set('');
    this.addEntryError.set(null);
  }

  protected closeAddHistoryEntry(): void {
    this.addingHistoryEntry.set(false);
    this.addEntryError.set(null);
  }

  protected onNewEntryDateChange(event: Event): void {
    this.newEntryDate.set((event.target as HTMLInputElement).value);
  }

  protected onNewEntryMemberChange(event: Event): void {
    this.newEntryMemberId.set((event.target as HTMLSelectElement).value);
  }

  protected canSaveNewEntry(): boolean {
    return Boolean(this.newEntryDate()) && Boolean(this.newEntryMemberId());
  }

  /**
   * Persist a brand-new completion for any date that's a valid occurrence
   * of the task. The existing PUT endpoint upserts: if the admin picks a
   * date that *already* has a completion (shouldn't happen because the
   * picker is intended for missing days), it's just an attribution
   * change. If the date isn't a valid occurrence of the task (e.g.
   * before startDate, or a wrong weekday for Weekly), the backend
   * returns 400 and we surface the message.
   */
  protected saveNewHistoryEntry(): void {
    const taskId = this.initial()?.id;
    const date = this.newEntryDate();
    const memberId = this.newEntryMemberId();
    if (!taskId || !date || !memberId) return;

    this.savingHistoryRow.set(true);
    this.addEntryError.set(null);
    this.api
      .setCompletionAttribution(taskId, date, { completedByMemberId: memberId })
      .subscribe({
        next: () => {
          // Splice the new entry into the list at the right position
          // (descending by date) so the user sees it without a refetch.
          this.completions.update((list) => {
            const without = list.filter((c) => c.occurrenceDate !== date);
            const next: TaskCompletionEntry = {
              occurrenceDate: date,
              completedByMemberId: memberId,
              completedAt: new Date().toISOString(),
              note: null,
            };
            return [...without, next].sort((a, b) =>
              a.occurrenceDate < b.occurrenceDate ? 1 : -1,
            );
          });
          this.savingHistoryRow.set(false);
          this.closeAddHistoryEntry();
        },
        error: () => {
          this.savingHistoryRow.set(false);
          this.addEntryError.set(
            $localize`:@@task-form.history.add-error:No se pudo añadir el completado. Comprueba que la fecha esté dentro del calendario de la tarea.`,
          );
        },
      });
  }

  /** Undo a single history row — calls the existing undo endpoint. */
  protected onUndoHistoryRow(date: string): void {
    const taskId = this.initial()?.id;
    if (!taskId) return;
    const confirmMsg = $localize`:@@task-form.history.undo-confirm:Quitar el completado del ${this.historyRowLabel(date)}:DATE:?`;
    if (!window.confirm(confirmMsg)) return;

    this.savingHistoryRow.set(true);
    this.api.undoOccurrence(taskId, date).subscribe({
      next: () => {
        this.completions.update((list) => list.filter((c) => c.occurrenceDate !== date));
        this.savingHistoryRow.set(false);
      },
      error: () => this.savingHistoryRow.set(false),
    });
  }

  /** Toggle floating from the form checkbox. Wipes recurrence/dueDate inputs in lockstep. */
  protected setFloating(value: boolean): void {
    this.isFloating.set(value);
    if (value) {
      this.recurrence.set('None');
      this.dueDate = '';
      this.selectedDays.set([]);
    }
  }

  private todayIso(): string {
    const now = new Date();
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, '0');
    const d = String(now.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }
}
