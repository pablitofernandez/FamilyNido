import { NgClass, NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  LOCALE_ID,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Subject, debounceTime, firstValueFrom, switchMap } from 'rxjs';

import { MealsService } from '../../core/api/meals.service';
import { MealCourse, MealPlanSlotEntry, MealSlot, MealWeek } from '../../core/models/meal';
import { IconComponent } from '../../shared/ui/icon/icon.component';

/** Logical key identifying which course of which slot is being edited. */
interface CellKey {
  date: string;
  slot: MealSlot;
  course: MealCourse;
}

/** Row used to render a single day in the template. */
interface GridDay {
  date: string;
  /** Friendly label ("lun 28 abr"). */
  label: string;
  /** True when the date is today (highlighted in the UI). */
  isToday: boolean;
  lunch: MealPlanSlotEntry | null;
  dinner: MealPlanSlotEntry | null;
}

/**
 * "La mesa" — weekly meal planner. Each day has two slots (comida/cena) and
 * each slot carries up to two free-text courses (primer plato, segundo plato).
 * Both courses are optional; clicking on a course opens an inline editor with
 * autocomplete from the family's history.
 */
@Component({
  selector: 'fn-meals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent, NgClass, NgTemplateOutlet],
  templateUrl: './meals.component.html',
  styleUrl: './meals.component.css',
})
export class MealsComponent implements OnInit {
  private readonly api = inject(MealsService);

  /**
   * User-facing slot/course labels surfaced via ng-template context. They live
   * here (not as literals in the template) so $localize can pick them up at
   * compile time and emit one trans-unit per stable id.
   */
  protected readonly lunchLabel = $localize`:@@meals.slot.lunch:Comida`;
  protected readonly dinnerLabel = $localize`:@@meals.slot.dinner:Cena`;
  protected readonly firstCoursePlaceholder = $localize`:@@meals.course.first-placeholder:+ primero`;
  protected readonly secondCoursePlaceholder = $localize`:@@meals.course.second-placeholder:+ segundo`;

  private readonly locale = inject(LOCALE_ID);

  private readonly dayFormatter = new Intl.DateTimeFormat(this.locale, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
  });

  private readonly rangeFormatter = new Intl.DateTimeFormat(this.locale, {
    day: 'numeric',
    month: 'long',
  });

  /** Monday of the displayed week (YYYY-MM-DD). */
  protected readonly weekStart = signal(MealsComponent.thisMonday());
  protected readonly week = signal<MealWeek | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Editor state: which (date, slot, course) is being edited and its draft value. */
  protected readonly editingCell = signal<CellKey | null>(null);
  protected readonly draft = signal('');
  protected readonly suggestions = signal<string[]>([]);
  protected readonly suggestionsOpen = signal(false);
  /** Index of the currently highlighted suggestion (-1 = none). */
  protected readonly highlightIndex = signal(-1);

  /** Friendly label for the displayed week ("28 abril – 4 mayo"). */
  protected readonly weekLabel = computed(() => {
    const start = new Date(this.weekStart() + 'T00:00:00');
    const end = new Date(start);
    end.setDate(end.getDate() + 6);
    return `${this.rangeFormatter.format(start)} – ${this.rangeFormatter.format(end)}`;
  });

  /** Days projected for the template (label + isToday + slots). */
  protected readonly grid = computed<GridDay[]>(() => {
    const week = this.week();
    if (!week) return [];
    const todayIso = MealsComponent.toIso(new Date());
    return week.days.map((d) => ({
      date: d.date,
      label: this.dayFormatter.format(new Date(d.date + 'T00:00:00')),
      isToday: d.date === todayIso,
      lunch: d.lunch,
      dinner: d.dinner,
    }));
  });

  /** True when the displayed week is the current ISO week. */
  protected readonly isThisWeek = computed(() => this.weekStart() === MealsComponent.thisMonday());

  /** Triggers a debounced suggestions fetch as the user types. */
  private readonly prefixChanges = new Subject<string>();

  /**
   * Reference to the single visible draft input. Used by the focus effect
   * below to move focus reliably when the editor advances between courses
   * (autofocus alone is not honored on dynamic DOM mounts).
   */
  private readonly draftInput = viewChild<ElementRef<HTMLInputElement>>('draftInput');

  constructor() {
    // When the editing cell changes to a non-null value, defer focus to the
    // newly mounted input. queueMicrotask is enough because Angular's signal
    // change detection has already flushed the DOM by the time it runs.
    effect(() => {
      const cell = this.editingCell();
      if (cell === null) return;
      queueMicrotask(() => {
        const el = this.draftInput()?.nativeElement;
        if (el && document.activeElement !== el) {
          el.focus();
          el.select();
        }
      });
    });
  }

  ngOnInit(): void {
    void this.load();

    // Debounce typing → backend autocomplete.
    this.prefixChanges
      .pipe(
        debounceTime(120),
        switchMap((prefix) => this.api.suggestions(prefix, 8)),
      )
      .subscribe({
        next: (list) => {
          this.suggestions.set(list);
          // Reset the highlight when the list changes — the previous index may
          // point past the new bounds, and starting at "no highlight" matches
          // the "type then arrow-down to navigate" expectation.
          this.highlightIndex.set(-1);
        },
        error: () => {
          this.suggestions.set([]);
          this.highlightIndex.set(-1);
        },
      });
  }

  // ─── navigation ────────────────────────────────────────────────────────────

  protected previousWeek(): void {
    this.shiftWeek(-7);
  }

  protected nextWeek(): void {
    this.shiftWeek(7);
  }

  protected jumpToToday(): void {
    if (this.isThisWeek()) return;
    this.weekStart.set(MealsComponent.thisMonday());
    void this.load();
  }

  private shiftWeek(deltaDays: number): void {
    const current = new Date(this.weekStart() + 'T00:00:00');
    current.setDate(current.getDate() + deltaDays);
    this.weekStart.set(MealsComponent.toIso(current));
    void this.load();
  }

  // ─── editing ───────────────────────────────────────────────────────────────

  /** Returns the current value of a course inside a slot, if any. */
  protected courseValue(slot: MealPlanSlotEntry | null, course: MealCourse): string | null {
    if (!slot) return null;
    return course === 'First' ? slot.firstCourse : slot.secondCourse;
  }

  protected startEdit(date: string, slot: MealSlot, course: MealCourse, current: string | null): void {
    this.editingCell.set({ date, slot, course });
    this.draft.set(current ?? '');
    this.suggestions.set([]);
    this.suggestionsOpen.set(false);
    this.highlightIndex.set(-1);
    // Pre-populate suggestions immediately with the latest history.
    this.prefixChanges.next('');
    this.suggestionsOpen.set(true);
  }

  protected isEditing(date: string, slot: MealSlot, course: MealCourse): boolean {
    const cell = this.editingCell();
    return cell !== null && cell.date === date && cell.slot === slot && cell.course === course;
  }

  protected onDraftInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.draft.set(value);
    this.suggestionsOpen.set(true);
    this.highlightIndex.set(-1);
    this.prefixChanges.next(value);
  }

  protected isHighlighted(index: number): boolean {
    return this.highlightIndex() === index;
  }

  protected pickSuggestion(name: string): void {
    this.draft.set(name);
    void this.commit();
  }

  /** Re-entrancy guard so the blur fired by a re-render does not double-commit. */
  private committing = false;

  /**
   * Persists the current draft. When <paramref name="advanceTo"/> is set, the
   * editor reopens on that course of the same slot — used by Tab/Enter to
   * chain edits across primero/segundo without a click in between.
   */
  protected async commit(advanceTo: MealCourse | null = null): Promise<void> {
    if (this.committing) return;
    const cell = this.editingCell();
    if (!cell) return;

    this.committing = true;
    const name = this.draft().trim();
    try {
      if (name.length === 0) {
        await firstValueFrom(this.api.clear(cell.date, cell.slot, cell.course));
      } else {
        await firstValueFrom(this.api.upsert({
          date: cell.date,
          slot: cell.slot,
          course: cell.course,
          name,
        }));
      }
      await this.load();
    } catch {
      this.error.set('save');
    } finally {
      this.committing = false;
    }

    if (advanceTo) {
      // Open the next course on the same slot using the freshly loaded value.
      const week = this.week();
      const day = week?.days.find((d) => d.date === cell.date) ?? null;
      const entry = day === null ? null : (cell.slot === 'Lunch' ? day.lunch : day.dinner);
      this.startEdit(cell.date, cell.slot, advanceTo, this.courseValue(entry, advanceTo));
    } else {
      this.cancel();
    }
  }

  protected cancel(): void {
    this.editingCell.set(null);
    this.draft.set('');
    this.suggestions.set([]);
    this.suggestionsOpen.set(false);
    this.highlightIndex.set(-1);
  }

  protected onBlur(date: string, slot: MealSlot, course: MealCourse): void {
    if (this.committing) return;
    // When the cell has already advanced (Tab/Enter), the blur fired here is a
    // stray event from the destroyed input. Ignore it — committing on this
    // path would clobber the new draft the user just entered.
    const cell = this.editingCell();
    if (!cell || cell.date !== date || cell.slot !== slot || cell.course !== course) {
      return;
    }
    void this.commit();
  }

  protected onKeydown(event: KeyboardEvent): void {
    const cell = this.editingCell();
    if (!cell) return;

    const list = this.suggestions();
    const listOpen = this.suggestionsOpen() && list.length > 0;

    if (event.key === 'Escape') {
      event.preventDefault();
      // Esc first closes the suggestions panel; a second Esc cancels editing.
      if (listOpen) {
        this.suggestionsOpen.set(false);
        this.highlightIndex.set(-1);
      } else {
        this.cancel();
      }
      return;
    }

    if (listOpen && event.key === 'ArrowDown') {
      event.preventDefault();
      const next = this.highlightIndex() + 1;
      this.highlightIndex.set(next >= list.length ? 0 : next);
      return;
    }

    if (listOpen && event.key === 'ArrowUp') {
      event.preventDefault();
      const next = this.highlightIndex() - 1;
      this.highlightIndex.set(next < 0 ? list.length - 1 : next);
      return;
    }

    if (event.key === 'Enter') {
      event.preventDefault();
      // When a suggestion is highlighted the user clearly wants that one;
      // otherwise commit whatever they typed and chain to the next course.
      if (listOpen && this.highlightIndex() >= 0) {
        this.draft.set(list[this.highlightIndex()]);
      }
      const advance = cell.course === 'First' ? 'Second' : null;
      void this.commit(advance);
      return;
    }

    if (event.key === 'Tab') {
      event.preventDefault();
      if (listOpen && this.highlightIndex() >= 0) {
        this.draft.set(list[this.highlightIndex()]);
      }
      const advance: MealCourse | null = event.shiftKey
        ? (cell.course === 'Second' ? 'First' : null)
        : (cell.course === 'First' ? 'Second' : null);
      void this.commit(advance);
    }
  }

  // ─── duplicate previous ────────────────────────────────────────────────────

  protected async duplicatePrevious(): Promise<void> {
    const overwrite = window.confirm(
      'Si la semana actual ya tiene comidas planificadas, ¿quieres reemplazarlas? '
      + 'Acepta para reemplazar; cancela para conservar las existentes y rellenar solo huecos.',
    );

    try {
      const week = await firstValueFrom(
        this.api.duplicatePrevious({ weekStart: this.weekStart(), overwrite }),
      );
      this.week.set(week);
    } catch {
      this.error.set('duplicate');
    }
  }

  // ─── load ──────────────────────────────────────────────────────────────────

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const week = await firstValueFrom(this.api.week(this.weekStart()));
      this.week.set(week);
    } catch {
      this.error.set('load');
    } finally {
      this.loading.set(false);
    }
  }

  // ─── pure helpers ──────────────────────────────────────────────────────────

  private static toIso(date: Date): string {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private static thisMonday(): string {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const diff = (today.getDay() + 6) % 7; // Mon=0, Sun=6
    today.setDate(today.getDate() - diff);
    return MealsComponent.toIso(today);
  }
}
