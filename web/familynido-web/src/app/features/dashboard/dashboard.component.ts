import { DecimalPipe, NgClass, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, LOCALE_ID, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { CalendarService } from '../../core/api/calendar.service';
import { DashboardService } from '../../core/api/dashboard.service';
import { FamilyMembersService } from '../../core/api/family-members.service';
import { HouseholdTasksService } from '../../core/api/household-tasks.service';
import { MealsService } from '../../core/api/meals.service';
import { MemberAgendaService } from '../../core/api/member-agenda.service';
import { SchoolService } from '../../core/api/school.service';
import { ScoresService } from '../../core/api/scores.service';
import { WallService } from '../../core/api/wall.service';
import { WeatherService } from '../../core/api/weather.service';
import { AuthService } from '../../core/auth/auth.service';
import { ResolvedAgendaEntry } from '../../core/models/agenda';
import { CalendarEvent, GoogleAccount } from '../../core/models/calendar';
import { ScoreboardEntry } from '../../core/models/scores';
import { DashboardWidget, DashboardWidgetId } from '../../core/models/dashboard';
import { FamilyMember } from '../../core/models/family-member';
import { DayTasks, HouseholdTask, TaskOccurrence } from '../../core/models/household-task';
import { MealPlanSlotEntry } from '../../core/models/meal';
import { ResolvedExtracurricular, ResolvedSchoolDay, SchoolHoliday, TransportMode } from '../../core/models/school';
import { WeatherToday } from '../../core/models/weather';
import { WallMessage } from '../../core/models/wall';
import { refreshOnFocus } from '../../core/realtime/refresh-on-focus';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

/** Row used by the "tareas de hoy" widget. */
interface TaskRow {
  task: HouseholdTask;
  occurrence: TaskOccurrence;
}

/** Fallback widget order used until /api/dashboard/preferences resolves. Mirrors the backend catalogue. */
const DEFAULT_WIDGET_ORDER: DashboardWidgetId[] = [
  'weather', 'school', 'agenda', 'tasks', 'calendar', 'meals', 'wall', 'scores', 'birthdays',
];

/** Row used by the "cumpleaños próximos" widget. */
interface BirthdayRow {
  member: FamilyMember;
  /** ISO date of the upcoming birthday. */
  date: string;
  /** Pretty-printed label ("martes 28 abr"). */
  label: string;
  /** Age the member will turn. */
  age: number;
  /** Days from today (0 = today). */
  daysAhead: number;
}

/**
 * Dashboard / Inicio — agrega lo que más usa la familia en un día normal:
 * tareas pendientes, mensajes fijados del muro, próximos eventos del Google
 * Calendar sincronizado y cumpleaños próximos. Sin datos propios — pura
 * composición de los módulos existentes.
 */
@Component({
  selector: 'fn-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, IconComponent, AvatarComponent, NgClass, NgTemplateOutlet, DecimalPipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly tasks = inject(HouseholdTasksService);
  private readonly wall = inject(WallService);
  private readonly calendar = inject(CalendarService);
  private readonly meals = inject(MealsService);
  private readonly membersApi = inject(FamilyMembersService);
  private readonly weatherApi = inject(WeatherService);
  private readonly schoolApi = inject(SchoolService);
  private readonly dashboardApi = inject(DashboardService);
  private readonly agendaApi = inject(MemberAgendaService);
  private readonly scoresApi = inject(ScoresService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly locale = inject(LOCALE_ID);

  // Formatters honour the bundle's LOCALE_ID so date/time labels follow the
  // same language as the rest of the UI. They live as instance fields (not
  // statics) because the locale is DI-resolved and only available inside the
  // class.
  private readonly dateFormatter = new Intl.DateTimeFormat(this.locale, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });

  private readonly shortDateFormatter = new Intl.DateTimeFormat(this.locale, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
  });

  private readonly timeFormatter = new Intl.DateTimeFormat(this.locale, {
    hour: '2-digit',
    minute: '2-digit',
  });

  protected readonly todayLabel = computed(() => this.dateFormatter.format(new Date()));
  protected readonly displayName = computed(() => this.auth.me()?.displayName ?? '');
  protected readonly colorHex = computed(() => this.auth.me()?.colorHex ?? '#C96442');

  /** URL of the current user's own avatar photo, or null when no photo is set. */
  protected readonly photoUrl = computed(() => {
    const me = this.auth.me();
    return me?.photoPath && me.memberId ? `/api/family-members/${me.memberId}/photo` : null;
  });

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    if (hour < 12) return $localize`:@@dashboard.greeting.morning:Buenos días`;
    if (hour < 20) return $localize`:@@dashboard.greeting.afternoon:Buenas tardes`;
    return $localize`:@@dashboard.greeting.evening:Buenas noches`;
  });

  // ─── data signals ──────────────────────────────────────────────────────────
  protected readonly loading = signal(true);
  protected readonly today = signal<DayTasks | null>(null);
  protected readonly pinned = signal<WallMessage[]>([]);
  protected readonly events = signal<CalendarEvent[]>([]);
  protected readonly members = signal<FamilyMember[]>([]);
  protected readonly todayLunch = signal<MealPlanSlotEntry | null>(null);
  protected readonly todayDinner = signal<MealPlanSlotEntry | null>(null);
  protected readonly tomorrowLunch = signal<MealPlanSlotEntry | null>(null);
  protected readonly tomorrowDinner = signal<MealPlanSlotEntry | null>(null);
  protected readonly weather = signal<WeatherToday | null>(null);
  protected readonly todaySchoolDays = signal<ResolvedSchoolDay[]>([]);
  protected readonly todayExtras = signal<ResolvedExtracurricular[]>([]);
  protected readonly todayHoliday = signal<SchoolHoliday | null>(null);

  protected readonly hasSchoolToday = computed(() =>
    this.todayHoliday() !== null
    || this.todaySchoolDays().length > 0
    || this.todayExtras().length > 0);

  /** Resolved agenda entries for today (filled by load(); empty when none). */
  protected readonly todayAgenda = signal<ResolvedAgendaEntry[]>([]);

  /** Only the entries flagged as "fuera de casa" — what the widget renders. */
  protected readonly awayToday = computed(() =>
    this.todayAgenda().filter((e) => e.isAway));

  /** Top of the family scoreboard for the current ISO week. */
  protected readonly weekScores = signal<ScoreboardEntry[]>([]);

  /** Trim to the first three entries — that's the podium the widget renders. */
  protected readonly topScores = computed(() => this.weekScores().slice(0, 3));

  /** Linked Google accounts — used to surface revoked-token / sync errors as a banner. */
  protected readonly googleAccounts = signal<GoogleAccount[]>([]);

  /** Accounts currently in a broken state (revoked or last sync errored). */
  protected readonly brokenAccounts = computed<GoogleAccount[]>(() =>
    this.googleAccounts().filter((a) => a.isRevoked || a.lastError),
  );

  /**
   * User-defined widget order — populated from /api/dashboard/preferences on
   * load. Initialised to the catalogue's default order so the dashboard renders
   * something useful before the prefs request resolves (or if it fails).
   */
  protected readonly widgets = signal<DashboardWidget[]>(
    DEFAULT_WIDGET_ORDER.map((id) => ({ id, visible: true })),
  );

  /** Visible widgets in the user-defined order. */
  protected readonly visibleWidgets = computed<DashboardWidgetId[]>(() =>
    this.widgets().filter((w) => w.visible).map((w) => w.id),
  );

  /** Number of tasks still pending today (used by the header summary). */
  protected readonly pendingTodayCount = computed<number>(() => {
    const day = this.today();
    if (!day) return 0;
    return day.tasks.filter((row) => !row.occurrence.isCompleted).length;
  });

  /**
   * Tasks for today rendered by the widget: pending first, completed at the
   * bottom (line-through). Capped at 8 so the widget doesn't grow unbounded.
   */
  protected readonly todayTasks = computed<TaskRow[]>(() => {
    const day = this.today();
    if (!day) return [];
    const pending = day.tasks.filter((row) => !row.occurrence.isCompleted);
    const done = day.tasks.filter((row) => row.occurrence.isCompleted);
    return [...pending, ...done].slice(0, 8);
  });

  /** True when at least one course (lunch or dinner) is planned for today. */
  protected readonly hasTodayMeals = computed(() => DashboardComponent.hasAnyCourse(this.todayLunch(), this.todayDinner()));

  /** True when at least one course is planned for tomorrow. */
  protected readonly hasTomorrowMeals = computed(() => DashboardComponent.hasAnyCourse(this.tomorrowLunch(), this.tomorrowDinner()));

  /** True when neither today nor tomorrow has any course planned (shared empty state). */
  protected readonly hasAnyMeals = computed(() => this.hasTodayMeals() || this.hasTomorrowMeals());

  private static hasAnyCourse(lunch: MealPlanSlotEntry | null, dinner: MealPlanSlotEntry | null): boolean {
    return Boolean(
      lunch?.firstCourse || lunch?.secondCourse ||
      dinner?.firstCourse || dinner?.secondCourse,
    );
  }

  /** Top 5 eventos próximos (siguientes 7 días). */
  protected readonly upcomingEvents = computed<CalendarEvent[]>(() =>
    this.events()
      .slice()
      .sort((a, b) => a.startAt.localeCompare(b.startAt))
      .slice(0, 5),
  );

  /** Cumpleaños en los próximos 30 días, ordenados por fecha. */
  protected readonly upcomingBirthdays = computed<BirthdayRow[]>(() => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const horizon = 30;

    const rows: BirthdayRow[] = [];
    for (const member of this.members()) {
      if (!member.birthDate || !member.isActive) {
        continue;
      }

      const [year, month, day] = member.birthDate.split('-').map(Number);
      const thisYear = new Date(today.getFullYear(), month - 1, day);
      let target = thisYear;
      if (target < today) {
        target = new Date(today.getFullYear() + 1, month - 1, day);
      }
      const daysAhead = Math.round((target.getTime() - today.getTime()) / 86_400_000);
      if (daysAhead > horizon) {
        continue;
      }

      rows.push({
        member,
        date: target.toISOString().slice(0, 10),
        label: this.shortDateFormatter.format(target),
        age: target.getFullYear() - year,
        daysAhead,
      });
    }

    return rows.sort((a, b) => a.daysAhead - b.daysAhead);
  });

  /** Resumen breve para la cabecera. */
  protected readonly summaryLine = computed(() => {
    const pending = this.pendingTodayCount();
    const events = this.upcomingEvents().length;
    const parts: string[] = [];
    if (pending > 0) {
      parts.push(
        pending === 1
          ? $localize`:@@dashboard.summary.tasks-one:${pending}:N: tarea hoy`
          : $localize`:@@dashboard.summary.tasks-many:${pending}:N: tareas hoy`,
      );
    }
    if (events > 0) {
      parts.push(
        events === 1
          ? $localize`:@@dashboard.summary.events-one:${events}:N: evento próximos`
          : $localize`:@@dashboard.summary.events-many:${events}:N: eventos próximos`,
      );
    }
    if (parts.length === 0) {
      return $localize`:@@dashboard.summary.calm:Hoy parece tranquilo.`;
    }
    return parts.join(' · ');
  });

  // ─── i18n helpers (lifted from the template) ─────────────────────────────
  protected readonly undoAriaLabel = $localize`:@@dashboard.tasks.undo-aria:Deshacer`;
  protected readonly markDoneAriaLabel = $localize`:@@dashboard.tasks.done-aria:Marcar como hecha`;
  protected readonly todayLabelStr = $localize`:@@dashboard.meals.today:Hoy`;
  protected readonly tomorrowLabelStr = $localize`:@@dashboard.meals.tomorrow:Mañana`;
  protected readonly lunchLabelStr = $localize`:@@dashboard.meals.lunch:Comida`;
  protected readonly dinnerLabelStr = $localize`:@@dashboard.meals.dinner:Cena`;

  /** Banner that summarises broken Google accounts. */
  protected brokenAccountsLabel(): string {
    const broken = this.brokenAccounts();
    if (broken.length === 1) {
      const acc = broken[0];
      return acc.isRevoked
        ? $localize`:@@dashboard.broken-accounts.one-revoked:La cuenta de Google de ${acc.email}:EMAIL: tiene el acceso revocado.`
        : $localize`:@@dashboard.broken-accounts.one-error:La cuenta de Google de ${acc.email}:EMAIL: ha tenido un error al sincronizar.`;
    }
    return $localize`:@@dashboard.broken-accounts.many:${broken.length}:N: cuentas de Google necesitan atención.`;
  }

  /** "<holiday> — hoy no hay cole." */
  protected schoolHolidayLabel(label: string): string {
    return $localize`:@@dashboard.school.holiday:${label}:LABEL: — hoy no hay cole.`;
  }

  /**
   * One school-day row built as HTML so the strong/em tags survive translation
   * in a single trans-unit. Picks the right wording based on which adults are
   * assigned (dropoff, pickup, both, neither).
   */
  protected schoolDayRowHtml(b: ResolvedSchoolDay): string {
    const kid = `<strong>${this.escape(this.memberName(b.memberId))}</strong>`;
    const morning = b.morningTime ? this.formatSchoolTime(b.morningTime) : '';
    const afternoon = b.afternoonTime ? this.formatSchoolTime(b.afternoonTime) : '';

    if (!b.dropoffMemberId && !b.pickupMemberId) {
      return afternoon
        ? $localize`:@@dashboard.school.row.unassigned-time:${kid}:KID: · <em>sin asignar</em> · a las ${afternoon}:TIME:`
        : $localize`:@@dashboard.school.row.unassigned:${kid}:KID: · <em>sin asignar</em>`;
    }

    let html = kid;
    if (b.dropoffMemberId) {
      const drop = `<strong>${this.escape(this.memberName(b.dropoffMemberId))}</strong>`;
      html += morning
        ? $localize`:@@dashboard.school.row.drop-time: · lleva ${drop}:NAME: a las ${morning}:TIME:`
        : $localize`:@@dashboard.school.row.drop: · lleva ${drop}:NAME:`;
    }
    if (b.pickupMemberId) {
      const pick = `<strong>${this.escape(this.memberName(b.pickupMemberId))}</strong>`;
      html += afternoon
        ? $localize`:@@dashboard.school.row.pickup-time: · recoge ${pick}:NAME: a las ${afternoon}:TIME:`
        : $localize`:@@dashboard.school.row.pickup: · recoge ${pick}:NAME:`;
    }
    return html;
  }

  /** Extracurricular row, same approach as schoolDayRowHtml. */
  protected extracurricularRowHtml(e: ResolvedExtracurricular): string {
    const name = `<strong>${this.escape(e.name)}</strong>`;
    const kidName = this.escape(this.memberName(e.memberId));
    const start = this.formatSchoolTime(e.startTime);
    const end = this.formatSchoolTime(e.endTime);
    let html = $localize`:@@dashboard.school.extra.base:${name}:NAME: · ${kidName}:KID: · ${start}:START:–${end}:END:`;
    if (e.location) {
      html += ` · ${this.escape(e.location)}`;
    }
    if (!e.isCancelled && e.dropoffMemberId) {
      const drop = `<strong>${this.escape(this.memberName(e.dropoffMemberId))}</strong>`;
      html += $localize`:@@dashboard.school.extra.drop: · lleva ${drop}:NAME:`;
    }
    if (!e.isCancelled && e.pickupMemberId) {
      const pick = `<strong>${this.escape(this.memberName(e.pickupMemberId))}</strong>`;
      html += $localize`:@@dashboard.school.extra.pickup: · recoge ${pick}:NAME:`;
    }
    return html;
  }

  /** "X tarea" / "X tareas" for the scoreboard. */
  protected scoreCountLabel(count: number): string {
    return count === 1
      ? $localize`:@@dashboard.scores.tasks-one:${count}:N: tarea`
      : $localize`:@@dashboard.scores.tasks-many:${count}:N: tareas`;
  }

  /** "X años" — used in the birthday widget. */
  protected ageLabel(age: number): string {
    return $localize`:@@dashboard.birthdays.age:${age}:N: años`;
  }

  /** Minimal HTML escaper used by the school-row html builders. */
  private escape(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  ngOnInit(): void {
    void this.load();
    // Refresh the whole panel when the user comes back to the tab — covers
    // the "I closed the phone, came back, what's new?" flow without keeping a
    // persistent connection.
    refreshOnFocus(() => void this.load(), this.destroyRef);
  }

  // ─── interactions ──────────────────────────────────────────────────────────

  protected async toggleTask(row: TaskRow): Promise<void> {
    const day = this.today();
    if (!day) return;

    const date = day.date;
    const call$ = row.occurrence.isCompleted
      ? this.tasks.undoOccurrence(row.task.id, date)
      : this.tasks.completeOccurrence(row.task.id, date);

    try {
      const updated = await firstValueFrom(call$);
      const apply = () => this.today.update((current) => {
        if (!current) return current;
        return {
          ...current,
          tasks: current.tasks.map((t) =>
            t.task.id === row.task.id
              ? { task: t.task, occurrence: updated }
              : t,
          ),
        };
      });

      // Wrap the mutation in a view transition when the browser supports it
      // (Chromium, Safari 18+). The browser snapshots the list before/after
      // and animates each row with a matching `view-transition-name` from its
      // old position to its new one — exactly what we want when a task moves
      // from "pendientes" to "hechas hoy". Falls back to a plain update.
      const startViewTransition = (document as Document & {
        startViewTransition?: (cb: () => void) => unknown;
      }).startViewTransition;
      if (typeof startViewTransition === 'function') {
        startViewTransition.call(document, apply);
      } else {
        apply();
      }

      // The scoreboard widget on the same screen needs to reflect the new
      // points right away — fire and forget, the call is cheap.
      void this.refreshLeaderboard();
    } catch {
      // silent — the ToastService is a phase-2 concern
    }
  }

  /** Refetch the scoreboard for the current ISO week. Best-effort. */
  private async refreshLeaderboard(): Promise<void> {
    const now = new Date();
    const todayIso = DashboardComponent.toIso(now);
    const todayMonday = DashboardComponent.weekStartIso(now);
    try {
      const board = await firstValueFrom(this.scoresApi.leaderboard(todayMonday, todayIso));
      this.weekScores.set(board.entries);
    } catch {
      // silent
    }
  }

  // ─── display helpers ───────────────────────────────────────────────────────

  protected memberName(memberId: string | null): string {
    if (!memberId) return '';
    return this.members().find((m) => m.id === memberId)?.displayName ?? '';
  }

  protected memberColor(memberId: string | null): string {
    if (!memberId) return 'var(--color-terra)';
    return this.members().find((m) => m.id === memberId)?.colorHex ?? '#999999';
  }

  /** Avatar URL for the given member, or null when no photo is set. */
  protected memberPhotoUrl(memberId: string | null): string | null {
    if (!memberId) return null;
    const m = this.members().find((x) => x.id === memberId);
    return m?.photoPath ? `/api/family-members/${m.id}/photo` : null;
  }

  protected eventColorFor(event: CalendarEvent): string {
    return this.memberColor(event.familyMemberId);
  }

  protected eventTimeLabel(event: CalendarEvent): string {
    if (event.isAllDay) {
      return 'Todo el día';
    }
    return this.timeFormatter.format(new Date(event.startAt));
  }

  protected eventDayLabel(event: CalendarEvent): string {
    return this.shortDateFormatter.format(new Date(event.startAt));
  }

  protected formatSchoolTime(value: string): string {
    return value.length >= 5 ? value.slice(0, 5) : value;
  }

  /** Single emoji that represents the member's transport mode in the agenda widget. */
  protected agendaTransportIcon(mode: ResolvedAgendaEntry['transportMode']): string {
    switch (mode) {
      case 'Car': return '🚗';
      case 'Bus': return '🚌';
      case 'Walk': return '🚶';
      case 'Train': return '🚆';
      case 'Plane': return '✈️';
      case 'Other': return '🧭';
      default: return '';
    }
  }

  /** Compact "HH:mm – HH:mm" or "HH:mm →" / "→ HH:mm" / "Todo el día" for the agenda widget. */
  protected agendaTimeLabel(entry: ResolvedAgendaEntry): string {
    const fmt = (t: string | null) => t ? t.slice(0, 5) : '';
    if (!entry.startTime && !entry.endTime) return 'Todo el día';
    if (entry.startTime && !entry.endTime) return `${fmt(entry.startTime)} →`;
    if (!entry.startTime && entry.endTime) return `→ ${fmt(entry.endTime)}`;
    return `${fmt(entry.startTime)} – ${fmt(entry.endTime)}`;
  }

  /** Single emoji that represents the kid's transport mode in the school widget. */
  protected schoolModeIcon(mode: TransportMode): string {
    switch (mode) {
      case 'Bus': return '🚌';
      case 'Walk': return '🚶';
      case 'Car': return '🚗';
      default: return '🎓';
    }
  }

  protected birthdayHint(row: BirthdayRow): string {
    if (row.daysAhead === 0) return '¡Hoy!';
    if (row.daysAhead === 1) return 'Mañana';
    return `En ${row.daysAhead} días`;
  }

  protected assigneesLabel(task: HouseholdTask): string {
    // The dashboard widget cares about "who's doing this today". Surfacing
    // the singular responsible matches that intent; if there's no
    // responsible we fall back to the related members so a task like
    // "ir a recoger a Bob" still shows useful context.
    if (task.responsibleMemberId) {
      const name = this.memberName(task.responsibleMemberId);
      return name.length > 0 ? name : $localize`:@@dashboard.tasks.no-responsible:Sin responsable`;
    }
    if (task.relatedMemberIds.length > 0) {
      const related = task.relatedMemberIds
        .map((id) => this.memberName(id))
        .filter((n) => n.length > 0)
        .join(' · ');
      return $localize`:@@dashboard.tasks.related-prefix:Para ${related}:NAMES:`;
    }
    return $localize`:@@dashboard.tasks.no-responsible:Sin responsable`;
  }

  /** Subtitle for the row used by the widget — switches to "✓ por X" once done. */
  protected rowSubtitle(row: TaskRow): string {
    if (row.occurrence.isCompleted && row.occurrence.completedByMemberId) {
      const name = this.memberName(row.occurrence.completedByMemberId);
      return name
        ? $localize`:@@dashboard.tasks.completed-by:✓ por ${name}:NAME:`
        : $localize`:@@dashboard.tasks.completed:✓ hecha`;
    }
    return this.assigneesLabel(row.task);
  }

  /** Stable per-row name used by the View Transitions API to animate moves. */
  protected viewTransitionName(taskId: string): string {
    // CSS idents allow [a-zA-Z0-9-_]; UUIDs are already valid after the prefix.
    return `task-${taskId}`;
  }

  // ─── load + live update helpers ────────────────────────────────────────────

  private async load(): Promise<void> {
    this.loading.set(true);

    const now = new Date();
    const horizon = new Date(now);
    horizon.setDate(horizon.getDate() + 7);

    const tomorrow = new Date(now);
    tomorrow.setDate(tomorrow.getDate() + 1);
    const todayIso = DashboardComponent.toIso(now);
    const tomorrowIso = DashboardComponent.toIso(tomorrow);

    // The meals API returns a Monday-anchored week. Tomorrow falls in the same
    // week as today every day except Sunday → Monday — handle that one case
    // with a second request so we never miss the next day's plan.
    const todayMonday = DashboardComponent.weekStartIso(now);
    const tomorrowMonday = DashboardComponent.weekStartIso(tomorrow);

    try {
      const baseFetches = [
        firstValueFrom(this.tasks.today()),
        firstValueFrom(this.wall.list({ limit: 1 })),
        firstValueFrom(this.calendar.listEvents({ from: now, to: horizon })),
        firstValueFrom(this.membersApi.list()),
        firstValueFrom(this.meals.week(todayMonday)),
        firstValueFrom(this.dashboardApi.getPreferences()),
      ] as const;

      const tomorrowFetch = todayMonday === tomorrowMonday
        ? null
        : firstValueFrom(this.meals.week(tomorrowMonday));

      const [today, feed, events, members, thisWeek, prefs] = await Promise.all(baseFetches);
      const nextWeek = tomorrowFetch === null ? thisWeek : await tomorrowFetch;

      this.today.set(today);
      this.pinned.set(feed.pinned);
      this.events.set(events);
      this.members.set(members);
      this.widgets.set(prefs.widgets);

      const todayMeal = thisWeek.days.find((d) => d.date === todayIso) ?? null;
      this.todayLunch.set(todayMeal?.lunch ?? null);
      this.todayDinner.set(todayMeal?.dinner ?? null);

      const tomorrowMeal = nextWeek.days.find((d) => d.date === tomorrowIso) ?? null;
      this.tomorrowLunch.set(tomorrowMeal?.lunch ?? null);
      this.tomorrowDinner.set(tomorrowMeal?.dinner ?? null);

      // Weather is best-effort; the widget hides itself when this stays null.
      try {
        const weather = await firstValueFrom(this.weatherApi.today());
        this.weather.set(weather);
      } catch {
        this.weather.set(null);
      }

      // School snapshot for today — also best-effort, hides when empty.
      try {
        const school = await firstValueFrom(this.schoolApi.overview(todayIso, todayIso));
        this.todaySchoolDays.set(school.resolvedDays.filter((b) => b.date === todayIso));
        this.todayExtras.set(school.resolvedExtracurriculars.filter((e) => e.date === todayIso));
        this.todayHoliday.set(school.holidays.find((h) => h.startDate <= todayIso && h.endDate >= todayIso) ?? null);
      } catch {
        this.todaySchoolDays.set([]);
        this.todayExtras.set([]);
        this.todayHoliday.set(null);
      }

      // Member agenda snapshot for today — drives the "Hoy fuera de casa" widget.
      try {
        const agenda = await firstValueFrom(this.agendaApi.overview(todayIso, todayIso));
        this.todayAgenda.set(agenda.resolved);
      } catch {
        this.todayAgenda.set([]);
      }

      // Family scoreboard for the current ISO week.
      try {
        const board = await firstValueFrom(this.scoresApi.leaderboard(todayMonday, todayIso));
        this.weekScores.set(board.entries);
      } catch {
        this.weekScores.set([]);
      }

      // Google account health — drives the "calendar broken" banner. Best-effort.
      try {
        const accounts = await firstValueFrom(this.calendar.listAccounts());
        this.googleAccounts.set(accounts);
      } catch {
        this.googleAccounts.set([]);
      }
    } catch {
      // Silent — partial data is still useful, ToastService is a phase-2 concern.
    } finally {
      this.loading.set(false);
    }
  }

  // ─── pure date helpers ─────────────────────────────────────────────────────

  /** Format a Date as YYYY-MM-DD in local time (matches the meal API contract). */
  private static toIso(date: Date): string {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  /** Monday (ISO week start) of the given date, formatted as YYYY-MM-DD. */
  private static weekStartIso(date: Date): string {
    const monday = new Date(date);
    monday.setHours(0, 0, 0, 0);
    const diff = (monday.getDay() + 6) % 7; // Mon=0, Sun=6
    monday.setDate(monday.getDate() - diff);
    return DashboardComponent.toIso(monday);
  }
}
