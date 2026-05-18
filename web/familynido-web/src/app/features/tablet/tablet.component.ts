import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, LOCALE_ID, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { CalendarService } from '../../core/api/calendar.service';
import { FamilyMembersService } from '../../core/api/family-members.service';
import { HouseholdTasksService } from '../../core/api/household-tasks.service';
import { MealsService } from '../../core/api/meals.service';
import { SchoolService } from '../../core/api/school.service';
import { WallService } from '../../core/api/wall.service';
import { WeatherService } from '../../core/api/weather.service';
import { AuthService } from '../../core/auth/auth.service';
import { projectWeather, reformatHourMinute, temperatureUnit } from '../../core/locale/format-prefs';
import { CalendarEvent } from '../../core/models/calendar';
import { FamilyMember } from '../../core/models/family-member';
import { DayTasks, HouseholdTask, TaskOccurrence } from '../../core/models/household-task';
import { MealPlanSlotEntry } from '../../core/models/meal';
import { ResolvedExtracurricular, ResolvedSchoolDay, SchoolHoliday, TransportMode } from '../../core/models/school';
import { WallMessage } from '../../core/models/wall';
import { WeatherToday } from '../../core/models/weather';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

/** Logical pages of the rotating tablet view. */
type Page = 'home' | 'cole' | 'tasks' | 'calendar' | 'meals' | 'wall';

interface BirthdayRow {
  member: FamilyMember;
  daysAhead: number;
  age: number;
}

interface TaskRow {
  task: HouseholdTask;
  occurrence: TaskOccurrence;
}

/**
 * "Modo tablet" — fullscreen ambient dashboard for the tablet at the entrance
 * of the house (RF-DASH-006). Lives outside the regular shell to ditch the
 * navigation chrome, rotates through six big-typography pages, refreshes data
 * every five minutes, and asks the browser to keep the screen awake while the
 * page is in the foreground.
 */
@Component({
  selector: 'fn-tablet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent, DatePipe, DecimalPipe, IconComponent],
  templateUrl: './tablet.component.html',
  styleUrl: './tablet.component.css',
})
export class TabletComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly tasksApi = inject(HouseholdTasksService);
  private readonly wallApi = inject(WallService);
  private readonly calendarApi = inject(CalendarService);
  private readonly mealsApi = inject(MealsService);
  private readonly membersApi = inject(FamilyMembersService);
  private readonly weatherApi = inject(WeatherService);
  private readonly schoolApi = inject(SchoolService);

  /** Auto-rotation cadence in ms — 18s per page is leisurely on a tablet. */
  private static readonly ROTATE_MS = 18_000;
  /** Background refresh cadence in ms — every 2 min for an ambient display. */
  private static readonly REFRESH_MS = 2 * 60_000;
  /** Clock tick — the visible HH:mm updates on this cadence. */
  private static readonly CLOCK_MS = 30_000;

  protected readonly pages: { id: Page; label: string }[] = [
    { id: 'home', label: $localize`:@@tablet.page.home:Hoy` },
    { id: 'cole', label: $localize`:@@tablet.page.school:Cole` },
    { id: 'tasks', label: $localize`:@@tablet.page.tasks:Tareas` },
    { id: 'calendar', label: $localize`:@@tablet.page.calendar:Agenda` },
    { id: 'meals', label: $localize`:@@tablet.page.meals:Mesa` },
    { id: 'wall', label: $localize`:@@tablet.page.wall:Muro` },
  ];

  protected readonly pageIndex = signal(0);
  protected readonly currentPage = computed(() => this.pages[this.pageIndex()].id);

  protected readonly now = signal(new Date());
  private readonly locale = inject(LOCALE_ID);

  // The big clock at the top of the tablet view goes through the locale-aware
  // formatter so US tablets show "9:42 PM" instead of "21:42". Issue #12.
  private readonly clockFormatter = new Intl.DateTimeFormat(this.locale, {
    hour: 'numeric',
    minute: '2-digit',
  });
  protected readonly clockTime = computed(() => this.clockFormatter.format(this.now()));

  protected readonly dateLabel = computed(() =>
    this.now().toLocaleDateString(this.locale, { weekday: 'long', day: 'numeric', month: 'long' }),
  );

  /** Same locale-aware formatter, reused to massage sunrise/sunset HH:mm strings. */
  protected readonly temperatureUnit = temperatureUnit(this.locale);

  protected readonly familyName = computed(() => this.auth.me()?.familyName ?? 'FamilyNido');

  // ─── data signals ──────────────────────────────────────────────────────────
  protected readonly loading = signal(true);
  protected readonly members = signal<FamilyMember[]>([]);
  protected readonly weather = signal<WeatherToday | null>(null);
  /** Weather payload projected into the user's preferred unit (issue #12). */
  protected readonly displayWeather = computed<WeatherToday | null>(() => {
    const w = this.weather();
    if (!w) return null;
    return projectWeather(
      w,
      this.temperatureUnit,
      (hhmm) => reformatHourMinute(hhmm, this.clockFormatter),
    );
  });
  protected readonly today = signal<DayTasks | null>(null);
  protected readonly events = signal<CalendarEvent[]>([]);
  protected readonly pinned = signal<WallMessage[]>([]);
  protected readonly recentMessages = signal<WallMessage[]>([]);
  protected readonly todayLunch = signal<MealPlanSlotEntry | null>(null);
  protected readonly todayDinner = signal<MealPlanSlotEntry | null>(null);
  protected readonly todaySchoolDays = signal<ResolvedSchoolDay[]>([]);
  protected readonly todayExtras = signal<ResolvedExtracurricular[]>([]);
  protected readonly todayHoliday = signal<SchoolHoliday | null>(null);

  protected readonly pendingTasks = computed<TaskRow[]>(() => {
    const day = this.today();
    if (!day) return [];
    return day.tasks.filter((row) => !row.occurrence.isCompleted).slice(0, 6);
  });

  protected readonly upcomingEvents = computed<CalendarEvent[]>(() =>
    this.events()
      .slice()
      .sort((a, b) => a.startAt.localeCompare(b.startAt))
      .slice(0, 5),
  );

  protected readonly upcomingBirthdays = computed<BirthdayRow[]>(() => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const out: BirthdayRow[] = [];
    for (const m of this.members()) {
      if (!m.isActive || !m.birthDate) continue;
      const [y, mo, d] = m.birthDate.split('-').map(Number);
      let target = new Date(today.getFullYear(), mo - 1, d);
      if (target < today) target = new Date(today.getFullYear() + 1, mo - 1, d);
      const days = Math.round((target.getTime() - today.getTime()) / 86_400_000);
      if (days > 30) continue;
      out.push({ member: m, daysAhead: days, age: target.getFullYear() - y });
    }
    return out.sort((a, b) => a.daysAhead - b.daysAhead);
  });

  protected readonly hasMeals = computed(() => {
    const lunch = this.todayLunch();
    const dinner = this.todayDinner();
    return Boolean(lunch?.firstCourse || lunch?.secondCourse || dinner?.firstCourse || dinner?.secondCourse);
  });

  // ─── lifecycle ─────────────────────────────────────────────────────────────

  private rotateTimer: ReturnType<typeof setInterval> | null = null;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  private clockTimer: ReturnType<typeof setInterval> | null = null;
  private wakeLock: WakeLockSentinel | null = null;

  async ngOnInit(): Promise<void> {
    await this.loadAll();

    // Periodic UI / data ticks. The rotation is paused on user interaction
    // (we restart it after each tap so a viewer can dwell on a page).
    this.rotateTimer = setInterval(() => this.advance(), TabletComponent.ROTATE_MS);
    this.refreshTimer = setInterval(() => void this.loadAll(), TabletComponent.REFRESH_MS);
    this.clockTimer = setInterval(() => this.now.set(new Date()), TabletComponent.CLOCK_MS);

    // Keep the screen on while the tab is visible. The Wake Lock API is
    // permission-free in modern browsers but only granted to user-visible
    // pages — releases automatically when the tab is hidden, and we re-ask
    // when it comes back to the foreground.
    void this.requestWakeLock();
    document.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  ngOnDestroy(): void {
    if (this.rotateTimer) clearInterval(this.rotateTimer);
    if (this.refreshTimer) clearInterval(this.refreshTimer);
    if (this.clockTimer) clearInterval(this.clockTimer);
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    void this.releaseWakeLock();
  }

  // ─── interaction ───────────────────────────────────────────────────────────

  /**
   * Tap anywhere on the page advances to the next slide and resets the timer
   * so it doesn't fire on top of the user's choice. Fingers-friendly.
   */
  protected onTap(): void {
    this.advance();
    this.resetRotateTimer();
  }

  protected goToPage(index: number): void {
    this.pageIndex.set(((index % this.pages.length) + this.pages.length) % this.pages.length);
    this.resetRotateTimer();
  }

  protected exit(): void {
    void this.router.navigateByUrl('/home');
  }

  private advance(): void {
    this.pageIndex.update((i) => (i + 1) % this.pages.length);
  }

  private resetRotateTimer(): void {
    if (this.rotateTimer) clearInterval(this.rotateTimer);
    this.rotateTimer = setInterval(() => this.advance(), TabletComponent.ROTATE_MS);
  }

  // ─── display helpers ───────────────────────────────────────────────────────

  protected memberName(memberId: string | null): string {
    if (!memberId) return '';
    return this.members().find((m) => m.id === memberId)?.displayName ?? '';
  }

  protected memberColor(memberId: string | null): string {
    if (!memberId) return '#999999';
    return this.members().find((m) => m.id === memberId)?.colorHex ?? '#999999';
  }

  protected memberPhotoUrl(memberId: string | null): string | null {
    if (!memberId) return null;
    const m = this.members().find((x) => x.id === memberId);
    return m?.photoPath ? `/api/family-members/${m.id}/photo` : null;
  }

  protected memberPhotoForMember(member: FamilyMember): string | null {
    return member.photoPath ? `/api/family-members/${member.id}/photo` : null;
  }

  protected formatTime(value: string): string {
    return value.length >= 5 ? value.slice(0, 5) : value;
  }

  protected eventTimeLabel(event: CalendarEvent): string {
    if (event.isAllDay) return $localize`:@@tablet.event.all-day:Todo el día`;
    const start = new Date(event.startAt);
    return `${pad(start.getHours())}:${pad(start.getMinutes())}`;
  }

  protected eventDayLabel(event: CalendarEvent): string {
    return new Date(event.startAt).toLocaleDateString(this.locale, { weekday: 'short', day: 'numeric', month: 'short' });
  }

  // ─── i18n string builders ────────────────────────────────────────────────

  /** "Hoy es <holiday> · sin cole" — home page banner. */
  protected holidayLine(label: string): string {
    return $localize`:@@tablet.holiday.line:Hoy es <strong>${this.escape(label)}:LABEL:</strong> · sin cole`;
  }

  /** Same but for the larger school page. */
  protected holidayLineLarge(label: string): string {
    return $localize`:@@tablet.holiday.line-large:<strong>${this.escape(label)}:LABEL:</strong> — hoy no hay cole.`;
  }

  /** "N tareas pendientes para hoy" / "1 tarea pendiente para hoy". */
  protected pendingTasksLabel(): string {
    const n = this.pendingTasks().length;
    return n === 1
      ? $localize`:@@tablet.tasks-pending.one:${n}:N: tarea pendiente para hoy`
      : $localize`:@@tablet.tasks-pending.many:${n}:N: tareas pendientes para hoy`;
  }

  /** "Mañana cumple <name> (age)". Uses birthdayHint for the day-relative part. */
  protected birthdayLine(row: BirthdayRow): string {
    const hint = this.escape(this.birthdayHint(row));
    const name = this.escape(row.member.displayName);
    return $localize`:@@tablet.birthday.line:${hint}:WHEN: cumple <strong>${name}:NAME:</strong> (${row.age}:AGE:)`;
  }

  /** "Lleva X · recoge Y" line under an extracurricular. */
  protected extraTransportLine(e: { dropoffMemberId: string | null; pickupMemberId: string | null }): string {
    const drop = e.dropoffMemberId ? this.memberName(e.dropoffMemberId) : '—';
    const pick = e.pickupMemberId ? this.memberName(e.pickupMemberId) : '—';
    return $localize`:@@tablet.extra.transport:Lleva ${drop}:DROP: · recoge ${pick}:PICK:`;
  }

  /** Single school-day row built as HTML so strong/em survive translation. */
  protected schoolDayLine(b: { memberId: string; dropoffMemberId: string | null; pickupMemberId: string | null; morningTime: string | null; afternoonTime: string | null }): string {
    const kid = `<strong>${this.escape(this.memberName(b.memberId))}</strong>`;
    const morning = b.morningTime ? this.formatTime(b.morningTime) : '';
    const afternoon = b.afternoonTime ? this.formatTime(b.afternoonTime) : '';
    let html = kid;
    if (b.dropoffMemberId) {
      const drop = `<strong>${this.escape(this.memberName(b.dropoffMemberId))}</strong>`;
      html += morning
        ? $localize`:@@tablet.school.row.drop-time: · lleva ${drop}:NAME: a las ${morning}:TIME:`
        : $localize`:@@tablet.school.row.drop: · lleva ${drop}:NAME:`;
    }
    if (b.pickupMemberId) {
      const pick = `<strong>${this.escape(this.memberName(b.pickupMemberId))}</strong>`;
      html += afternoon
        ? $localize`:@@tablet.school.row.pickup-time: · recoge ${pick}:NAME: a las ${afternoon}:TIME:`
        : $localize`:@@tablet.school.row.pickup: · recoge ${pick}:NAME:`;
    }
    return html;
  }

  private escape(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  protected birthdayHint(row: BirthdayRow): string {
    if (row.daysAhead === 0) return $localize`:@@tablet.birthday.hint.today:¡Hoy!`;
    if (row.daysAhead === 1) return $localize`:@@tablet.birthday.hint.tomorrow:Mañana`;
    return $localize`:@@tablet.birthday.hint.days:En ${row.daysAhead}:N: días`;
  }

  protected schoolModeIcon(mode: TransportMode): string {
    switch (mode) {
      case 'Bus': return '🚌';
      case 'Walk': return '🚶';
      case 'Car': return '🚗';
      default: return '🎓';
    }
  }

  /** Friendly greeting based on the local hour. */
  protected greeting(): string {
    const h = this.now().getHours();
    if (h < 12) return $localize`:@@dashboard.greeting.morning:Buenos días`;
    if (h < 20) return $localize`:@@dashboard.greeting.afternoon:Buenas tardes`;
    return $localize`:@@dashboard.greeting.evening:Buenas noches`;
  }

  // ─── loading ───────────────────────────────────────────────────────────────

  private async loadAll(): Promise<void> {
    if (this.members().length === 0) this.loading.set(true);
    try {
      const now = new Date();
      const horizon = new Date(now);
      horizon.setDate(horizon.getDate() + 7);
      const todayIso = toIso(now);
      const monday = mondayOf(now);

      const [members, today, feed, events, weekMeals] = await Promise.all([
        firstValueFrom(this.membersApi.list()),
        firstValueFrom(this.tasksApi.today()),
        firstValueFrom(this.wallApi.list({ limit: 20 })),
        firstValueFrom(this.calendarApi.listEvents({ from: now, to: horizon })),
        firstValueFrom(this.mealsApi.week(monday)),
      ]);

      this.members.set(members);
      this.today.set(today);
      this.pinned.set(feed.pinned);
      this.recentMessages.set(feed.messages.slice(0, 5));
      this.events.set(events);
      const todayMeal = weekMeals.days.find((d) => d.date === todayIso) ?? null;
      this.todayLunch.set(todayMeal?.lunch ?? null);
      this.todayDinner.set(todayMeal?.dinner ?? null);

      // Best-effort sub-fetches: each one falls back to empty rather than
      // breaking the whole tablet view if the upstream is down.
      try {
        const weather = await firstValueFrom(this.weatherApi.today());
        this.weather.set(weather);
      } catch {
        this.weather.set(null);
      }
      try {
        const overview = await firstValueFrom(this.schoolApi.overview(todayIso, todayIso));
        this.todaySchoolDays.set(overview.resolvedDays.filter((b) => b.date === todayIso));
        this.todayExtras.set(overview.resolvedExtracurriculars.filter((e) => e.date === todayIso));
        this.todayHoliday.set(overview.holidays.find((h) => h.startDate <= todayIso && h.endDate >= todayIso) ?? null);
      } catch {
        this.todaySchoolDays.set([]);
        this.todayExtras.set([]);
        this.todayHoliday.set(null);
      }
    } catch {
      // Silent — last-known state stays on screen.
    } finally {
      this.loading.set(false);
    }
  }

  // ─── wake lock ─────────────────────────────────────────────────────────────

  private async requestWakeLock(): Promise<void> {
    const nav = navigator as Navigator & { wakeLock?: { request(type: 'screen'): Promise<WakeLockSentinel> } };
    if (!nav.wakeLock) return;
    try {
      this.wakeLock = await nav.wakeLock.request('screen');
    } catch {
      // Browser denied or unsupported. The page still works without it.
    }
  }

  private async releaseWakeLock(): Promise<void> {
    try {
      await this.wakeLock?.release();
    } catch {
      // ignore
    } finally {
      this.wakeLock = null;
    }
  }

  /** Re-acquire the wake lock when the page comes back to the foreground. */
  private readonly onVisibilityChange = (): void => {
    if (document.visibilityState === 'visible' && this.wakeLock === null) {
      void this.requestWakeLock();
    }
  };
}

// ─── pure helpers ──────────────────────────────────────────────────────────

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function toIso(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function mondayOf(d: Date): string {
  const cursor = new Date(d);
  cursor.setHours(0, 0, 0, 0);
  const diff = (cursor.getDay() + 6) % 7;
  cursor.setDate(cursor.getDate() - diff);
  return toIso(cursor);
}
