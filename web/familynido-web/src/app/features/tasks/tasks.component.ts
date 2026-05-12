import { NgClass, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { FamilyMembersService } from '../../core/api/family-members.service';
import { HouseholdTasksService } from '../../core/api/household-tasks.service';
import { AuthService } from '../../core/auth/auth.service';
import { FamilyMember } from '../../core/models/family-member';
import { DayTasks, HouseholdTask, TaskOccurrence } from '../../core/models/household-task';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { TaskFormComponent, TaskFormResult } from './task-form.component';

/** Tabs offered at the top of the screen. */
type View = 'today' | 'week' | 'all';

/** Local row shape for the tab views — task + the specific date + its occurrence. */
interface TaskRow {
  task: HouseholdTask;
  date: string;
  occurrence: TaskOccurrence;
}

/**
 * "Las tareas" — shared household chores screen (RF-TASK-*).
 *
 * Three tabs (Hoy / Esta semana / Todas), inline quick-add, per-occurrence toggle,
 * and the full editor lives in {@link TaskFormComponent} (used as an embedded panel).
 * The component keeps everything in signals — consistent with `/nido`.
 */
@Component({
  selector: 'fn-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgClass, NgTemplateOutlet, AvatarComponent, IconComponent, TaskFormComponent],
  templateUrl: './tasks.component.html',
  styleUrl: './tasks.component.css',
})
export class TasksComponent implements OnInit {
  private readonly api = inject(HouseholdTasksService);
  private readonly membersApi = inject(FamilyMembersService);
  private readonly auth = inject(AuthService);
  private readonly locale = inject(LOCALE_ID);

  /** Caller is an admin — surfaces the completion section in the task form. */
  protected readonly isAdmin = computed(() => this.auth.me()?.role === 'Admin');

  protected readonly view = signal<View>('today');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly members = signal<FamilyMember[]>([]);
  protected readonly today = signal<DayTasks | null>(null);
  protected readonly weekTasks = signal<DayTasks[]>([]);
  protected readonly allTasks = signal<HouseholdTask[]>([]);

  protected readonly editing = signal(false);
  protected readonly editTarget = signal<HouseholdTask | null>(null);

  /** "Todas" tab: hide tasks that have at least one completion in their history. */
  protected readonly hideCompleted = signal(false);

  /**
   * Transient "+N points" hint shown over the checkbox when a task is just
   * completed. We only render one at a time — if a second tick lands within
   * the 1.2 s animation window it replaces the previous one. Cleared by a
   * timeout to avoid stale DOM.
   */
  protected readonly earned = signal<{ key: string; points: number } | null>(null);
  private earnedTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly tabs: { id: View; label: string }[] = [
    { id: 'today', label: $localize`:@@tasks.tab.today:Hoy` },
    { id: 'week', label: $localize`:@@tasks.tab.week:Esta semana` },
    { id: 'all', label: $localize`:@@tasks.tab.all:Todas` },
  ];

  /** Aria-labels surfaced through bracketed bindings — held as fields so $localize picks them up. */
  protected readonly newTaskAriaLabel = $localize`:@@tasks.new-task-aria:Nueva tarea`;
  protected readonly closeEditorAriaLabel = $localize`:@@tasks.close-editor-aria:Cerrar editor`;
  protected readonly undoAriaLabel = $localize`:@@tasks.undo-aria:Deshacer`;
  protected readonly completeAriaLabel = $localize`:@@tasks.complete-aria:Completar`;

  protected editAriaLabel(title: string): string {
    return $localize`:@@tasks.edit-aria:Editar ${title}:TITLE:`;
  }
  protected rewardTitle(points: number): string {
    return $localize`:@@tasks.reward-title:Recompensa: ${points}:POINTS: puntos`;
  }
  protected responsibleTitle(memberId: string): string {
    return $localize`:@@tasks.responsible-title:Responsable: ${this.memberName(memberId)}:NAME:`;
  }
  protected completedByLabel(memberId: string): string {
    return $localize`:@@tasks.completed-by:por ${this.memberName(memberId)}:NAME:`;
  }
  protected hiddenLabel(): string {
    const n = this.hiddenCompletedCount();
    return $localize`:@@tasks.hidden-count:${n}:N: oculta(s)`;
  }
  protected visibleCountLabel(): string {
    const n = this.visibleAllTasks().length;
    return n === 1
      ? $localize`:@@tasks.count-one:${n}:N: tarea`
      : $localize`:@@tasks.count-many:${n}:N: tareas`;
  }

  protected readonly subtitle = computed(() => {
    const pending = this.todayRows().filter((r) => !r.occurrence.isCompleted).length;
    if (pending === 0) {
      return $localize`:@@tasks.subtitle.empty:Nada urgente hoy`;
    }
    return pending === 1
      ? $localize`:@@tasks.subtitle.one:${pending}:N: pendiente hoy`
      : $localize`:@@tasks.subtitle.many:${pending}:N: pendientes hoy`;
  });

  protected readonly todayRows = computed<TaskRow[]>(() => {
    const day = this.today();
    return day ? this.toRows(day) : [];
  });

  /** Tasks for the "Todas" tab, applying the hide-completed toggle. */
  protected readonly visibleAllTasks = computed<HouseholdTask[]>(() =>
    this.hideCompleted()
      ? this.allTasks().filter((t) => t.latestCompletion === null)
      : this.allTasks(),
  );

  /** Number of tasks currently hidden by the toggle — used to label the button. */
  protected readonly hiddenCompletedCount = computed(() =>
    this.allTasks().filter((t) => t.latestCompletion !== null).length,
  );

  ngOnInit(): void {
    this.load();
  }

  protected toRows(day: DayTasks): TaskRow[] {
    return day.tasks.map(({ task, occurrence }) => ({ task, date: day.date, occurrence }));
  }

  protected toggle(row: TaskRow): void {
    const { task, date, occurrence } = row;
    const wasCompleted = occurrence.isCompleted;
    const call$ = wasCompleted
      ? this.api.undoOccurrence(task.id, date)
      : this.api.completeOccurrence(task.id, date);

    call$.subscribe({
      next: (updated) => {
        this.patchOccurrence(task.id, date, updated);
        if (!wasCompleted && updated.isCompleted) {
          this.flashEarned(task.id, date, task.points);
        }
      },
      error: () => this.error.set('toggle'),
    });
  }

  /** Key used to anchor the floating "+N" overlay to a specific row. */
  protected earnedKey(taskId: string, date: string): string {
    return `${taskId}|${date}`;
  }


  private flashEarned(taskId: string, date: string, points: number): void {
    if (this.earnedTimer) clearTimeout(this.earnedTimer);
    this.earned.set({ key: this.earnedKey(taskId, date), points });
    this.earnedTimer = setTimeout(() => this.earned.set(null), 1200);
  }

  protected archive(task: HouseholdTask): void {
    this.api.archive(task.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('archive'),
    });
  }

  protected restore(task: HouseholdTask): void {
    this.api.restore(task.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('restore'),
    });
  }

  /** Hard-delete a task (and its completion history) after confirmation. */
  protected delete(task: HouseholdTask): void {
    const msg = $localize`:@@tasks.delete-confirm:¿Eliminar definitivamente "${task.title}:TITLE:"? Se borrará junto a su historial de completados.`;
    if (!window.confirm(msg)) {
      return;
    }
    this.api.delete(task.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('delete'),
    });
  }

  protected edit(task: HouseholdTask, occurrence?: TaskOccurrence): void {
    // The today/week endpoints don't populate `latestCompletion` on the
    // task DTO — that field is only set by ListHouseholdTasks (the "Todas"
    // tab). When we open the form from a day row whose occurrence is
    // already completed, synthesize a latestCompletion out of the
    // occurrence so the form's "Estado" section reflects what the user
    // sees on screen.
    const enriched: HouseholdTask =
      task.latestCompletion === null && occurrence?.isCompleted && occurrence.completedAt
        ? {
            ...task,
            latestCompletion: {
              occurrenceDate: occurrence.occurrenceDate,
              completedByMemberId: occurrence.completedByMemberId,
              completedAt: occurrence.completedAt,
            },
          }
        : task;
    this.editTarget.set(enriched);
    this.editing.set(true);
    this.scrollToEditor();
  }

  protected toggleEditor(): void {
    if (this.editing()) {
      this.closeEditor();
    } else {
      this.editTarget.set(null);
      this.editing.set(true);
      this.scrollToEditor();
    }
  }

  /**
   * Bring the form into view after it mounts. The list is long enough that
   * clicking "Editar" on a row near the bottom would otherwise leave the
   * user staring at the same scroll position with no indication anything
   * happened — the panel renders at the top, off-screen.
   *
   * Defers via `requestAnimationFrame` so the form has a chance to mount
   * (the `@if (editing())` block is evaluated on the next change-detection
   * tick after the signal write).
   */
  private scrollToEditor(): void {
    requestAnimationFrame(() => {
      document
        .getElementById('task-form-anchor')
        ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  protected closeEditor(): void {
    this.editing.set(false);
    this.editTarget.set(null);
  }

  protected async onSave(result: TaskFormResult): Promise<void> {
    const { id, payload, completionOps } = result;
    try {
      // 1. Persist the regular task fields. We need the task id (either the
      //    one we already had on edit, or the freshly minted one on create)
      //    to chain the completion ops afterwards.
      const saved = id
        ? await firstValueFrom(this.api.update(id, payload))
        : await firstValueFrom(this.api.create(payload));

      // 2. Apply the admin-only completion delta. Order matters: undo any
      //    previous occurrence first so the upsert doesn't collide with a
      //    stale row at a different date.
      if (completionOps) {
        if (completionOps.toRemove) {
          await firstValueFrom(this.api.undoOccurrence(saved.id, completionOps.toRemove));
        }
        if (completionOps.toAdd) {
          await firstValueFrom(
            this.api.setCompletionAttribution(saved.id, completionOps.toAdd.date, {
              completedByMemberId: completionOps.toAdd.memberId,
            }),
          );
        }
      }

      this.closeEditor();
      this.load();
    } catch {
      this.error.set('save');
    }
  }

  protected recurrenceLabel(task: HouseholdTask): string {
    switch (task.recurrence) {
      case 'None':
        return task.dueDate
          ? $localize`:@@tasks.recurrence.once-on:Única · ${this.shortDate(task.dueDate)}:DATE:`
          : $localize`:@@tasks.recurrence.once:Única`;
      case 'Daily':
        return $localize`:@@tasks.recurrence.daily:Cada día`;
      case 'Weekly':
        return $localize`:@@tasks.recurrence.weekly:Semanal · ${this.weeklyLabel(task.weeklyDays)}:DAYS:`;
      case 'Monthly':
        if (task.monthlyDay === -1) {
          return $localize`:@@tasks.recurrence.monthly-last:Mensual · último día`;
        }
        return $localize`:@@tasks.recurrence.monthly-day:Mensual · día ${task.monthlyDay}:DAY:`;
    }
  }

  protected memberColor(memberId: string): string {
    return this.members().find((m) => m.id === memberId)?.colorHex ?? '#999999';
  }

  protected memberName(memberId: string): string {
    return this.members().find((m) => m.id === memberId)?.displayName ?? '—';
  }

  /** Avatar URL for a member, or null when no photo is set. */
  protected memberPhotoUrl(memberId: string | null): string | null {
    if (!memberId) return null;
    const m = this.members().find((x) => x.id === memberId);
    return m?.photoPath ? `/api/family-members/${m.id}/photo` : null;
  }

  protected dayLabel(date: string): string {
    const d = new Date(date + 'T00:00:00');
    return d.toLocaleDateString(this.locale, { weekday: 'long', day: 'numeric', month: 'short' });
  }

  private weeklyLabel(mask: string | null): string {
    if (!mask || mask === 'None') return '—';
    if (mask === 'Weekdays') return 'L–V';
    if (mask === 'Weekend') return 'S–D';
    if (mask === 'All') return 'todos';
    return mask
      .split(',')
      .map((s) => s.trim().slice(0, 1))
      .join('·');
  }

  private shortDate(date: string): string {
    const d = new Date(date + 'T00:00:00');
    return d.toLocaleDateString(this.locale, { day: 'numeric', month: 'short' });
  }

  private patchOccurrence(taskId: string, date: string, updated: TaskOccurrence): void {
    const patchDay = (day: DayTasks | null): DayTasks | null =>
      day === null
        ? null
        : {
            ...day,
            tasks: day.tasks.map((t) =>
              t.task.id === taskId && day.date === date
                ? { task: t.task, occurrence: updated }
                : t,
            ),
          };
    this.today.update(patchDay);
    this.weekTasks.update((days) => days.map((d) => patchDay(d)!));
  }

  /** "24 abr" — short date label used by the "Todas" tab. */
  protected shortDateLabel(date: string): string {
    return new Date(date + 'T00:00:00').toLocaleDateString(this.locale, {
      day: 'numeric',
      month: 'short',
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    Promise.all([
      firstValueFrom(this.membersApi.list()),
      firstValueFrom(this.api.today()),
      firstValueFrom(this.api.week()),
      firstValueFrom(this.api.list({ includeArchived: true })),
    ])
      .then(([members, today, week, all]) => {
        this.members.set(members);
        this.today.set(today);
        this.weekTasks.set(week);
        this.allTasks.set(all);
        this.loading.set(false);
      })
      .catch(() => {
        this.error.set('load');
        this.loading.set(false);
      });
  }
}
