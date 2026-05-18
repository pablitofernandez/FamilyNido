import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { CalendarService } from '../../core/api/calendar.service';
import { FamilyMembersService } from '../../core/api/family-members.service';
import { CalendarEvent } from '../../core/models/calendar';
import { FamilyMember } from '../../core/models/family-member';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

/** Local row shape — events grouped by day for the agenda view. */
interface DayBucket {
  /** YYYY-MM-DD in the user's local timezone (es-ES). */
  date: string;
  /** Pretty-printed label ("lunes 27 abr"). */
  label: string;
  events: CalendarEvent[];
}

/** One cell of the compact month grid rendered above the agenda. */
interface MonthDay {
  /** YYYY-MM-DD in the user's local timezone. */
  date: string;
  /** Day-of-month number shown in the cell. */
  dayNumber: number;
  /** False for the trailing days of the previous/next month (rendered dim). */
  isCurrentMonth: boolean;
  /** True when this cell represents today's date. */
  isToday: boolean;
  /** Distinct member-color hex strings for the day, max 3, used as colored dots. */
  dotColors: string[];
  /** Total number of events on this day — drives the optional "+N" overflow. */
  totalEvents: number;
}

/**
 * "El calendario" — agenda view of upcoming events synced from Google Calendar,
 * plus a compact month grid above it (RF-CAL-009) that surfaces per-day event
 * counts as member-coloured dots so the user can scan the month at a glance.
 * Tapping a day in the grid scrolls the agenda below to that bucket.
 *
 * The "Cuentas" button links to the linked-accounts settings where the user
 * manages the OAuth connections.
 */
@Component({
  selector: 'fn-calendar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, AvatarComponent, IconComponent, NgClass],
  templateUrl: './calendar.component.html',
  styleUrl: './calendar.component.css',
})
export class CalendarComponent implements OnInit {
  private readonly api = inject(CalendarService);
  private readonly membersApi = inject(FamilyMembersService);

  /**
   * Aria-label for a month-grid cell. Built here (not inline) so the two
   * branches go through $localize and the locale bundle picks up the
   * translation.
   */
  protected dayCellAriaLabel(cell: MonthDay): string {
    return cell.totalEvents === 0
      ? $localize`:@@calendar.day-cell.no-events:${cell.dayNumber}:DAY:, sin eventos`
      : $localize`:@@calendar.day-cell.with-events:${cell.dayNumber}:DAY:, ${cell.totalEvents}:COUNT: eventos`;
  }

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly events = signal<CalendarEvent[]>([]);
  protected readonly members = signal<FamilyMember[]>([]);

  /** Event currently open in the side drawer for member tagging. Null = closed. */
  protected readonly editingEvent = signal<CalendarEvent | null>(null);
  /** Working set of member ids inside the drawer (committed on Save). */
  protected readonly draftMemberIds = signal<string[]>([]);
  protected readonly savingDrawer = signal(false);
  protected readonly drawerError = signal<string | null>(null);

  /** Lookback window (relative to today). */
  protected readonly lookbackDays = 1;
  /** Lookahead window (relative to today). */
  protected readonly lookaheadDays = 35;

  private readonly locale = inject(LOCALE_ID);

  /** Static labels for the seven-column month grid header — same letters in es and en. */
  protected readonly weekdayInitials = ['L', 'M', 'X', 'J', 'V', 'S', 'D'];

  /** "abril 2026" / "April 2026" label for the month-grid header. */
  protected readonly monthLabel = computed(() =>
    new Date().toLocaleDateString(this.locale, { month: 'long', year: 'numeric' }),
  );

  /**
   * 6×7 grid covering the current month plus the leading/trailing days needed
   * to fill the first/last weeks. Each cell aggregates the events that fall on
   * that local date and exposes up to three member-coloured dots so the user
   * can scan the month at a glance — that's the visualization RF-CAL-009 asks
   * for.
   */
  protected readonly monthGrid = computed<MonthDay[]>(() => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const todayIso = this.toLocalDate(today.toISOString());

    const year = today.getFullYear();
    const month = today.getMonth(); // 0–11
    const firstOfMonth = new Date(year, month, 1);
    // Spanish weeks start on Monday — shift Sunday (0) to the end so monday=0.
    const startOffset = (firstOfMonth.getDay() + 6) % 7;
    const gridStart = new Date(firstOfMonth);
    gridStart.setDate(firstOfMonth.getDate() - startOffset);

    // Bucket events by local date once so the grid loop stays cheap.
    const eventsByDay = new Map<string, CalendarEvent[]>();
    for (const ev of this.events()) {
      const day = this.eventLocalDate(ev);
      const arr = eventsByDay.get(day);
      if (arr) arr.push(ev); else eventsByDay.set(day, [ev]);
    }

    const days: MonthDay[] = [];
    for (let i = 0; i < 42; i++) {
      const date = new Date(gridStart);
      date.setDate(gridStart.getDate() + i);
      const iso = this.toLocalDate(date.toISOString());
      const evs = eventsByDay.get(iso) ?? [];

      // Distinct colors in event order, capped at 3 — beyond that we display
      // a "+N" badge so a busy day doesn't turn into a soup of dots.
      const seen = new Set<string>();
      const dotColors: string[] = [];
      for (const ev of evs) {
        const color = this.memberColorFor(ev);
        if (seen.has(color)) continue;
        seen.add(color);
        dotColors.push(color);
        if (dotColors.length === 3) break;
      }

      days.push({
        date: iso,
        dayNumber: date.getDate(),
        isCurrentMonth: date.getMonth() === month,
        isToday: iso === todayIso,
        dotColors,
        totalEvents: evs.length,
      });
    }
    return days;
  });

  /** Scrolls the agenda below into view at the given day. Bound to month-grid taps. */
  protected scrollToDay(date: string): void {
    const target = document.getElementById(`agenda-day-${date}`);
    if (target) {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  /** Events grouped by local-day, ordered chronologically. */
  protected readonly buckets = computed<DayBucket[]>(() => {
    const events = this.events();
    if (events.length === 0) {
      return [];
    }

    const grouped = new Map<string, CalendarEvent[]>();
    for (const ev of events) {
      const day = this.eventLocalDate(ev);
      const list = grouped.get(day);
      if (list) {
        list.push(ev);
      } else {
        grouped.set(day, [ev]);
      }
    }

    const buckets: DayBucket[] = [];
    for (const [date, dayEvents] of grouped) {
      buckets.push({
        date,
        label: this.dayLabel(date),
        events: dayEvents.sort((a, b) => this.eventSortKey(a).localeCompare(this.eventSortKey(b))),
      });
    }

    return buckets.sort((a, b) => a.date.localeCompare(b.date));
  });

  protected readonly subtitle = computed((): string => {
    const total = this.events().length;
    if (total === 0) {
      return $localize`:@@calendar.subtitle.empty:Sin eventos próximos`;
    }
    return total === 1
      ? $localize`:@@calendar.subtitle.one:${total}:N: evento próximo`
      : $localize`:@@calendar.subtitle.many:${total}:N: eventos próximos`;
  });

  ngOnInit(): void {
    this.load();
  }

  protected refresh(): void {
    this.load();
  }

  protected memberColorFor(event: CalendarEvent): string {
    if (event.familyMemberId) {
      const member = this.members().find((m) => m.id === event.familyMemberId);
      if (member) {
        return member.colorHex;
      }
    }
    // Fallback to the FamilyNido neutral accent when no member is assigned.
    return 'var(--color-terra)';
  }

  protected timeLabel(event: CalendarEvent): string {
    if (event.isAllDay) {
      return $localize`:@@calendar.event.all-day:Todo el día`;
    }
    const start = new Date(event.startAt);
    const end = new Date(event.endAt);
    const fmt = (d: Date) =>
      d.toLocaleTimeString(this.locale, { hour: '2-digit', minute: '2-digit' });
    return `${fmt(start)}–${fmt(end)}`;
  }

  // ─── per-event member tagging drawer ───────────────────────────────────────

  protected openEvent(event: CalendarEvent): void {
    this.editingEvent.set(event);
    this.draftMemberIds.set([...event.relatedMemberIds]);
    this.drawerError.set(null);
  }

  protected closeDrawer(): void {
    this.editingEvent.set(null);
  }

  protected toggleDraftMember(id: string): void {
    this.draftMemberIds.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]);
  }

  protected isDraftMember(id: string): boolean {
    return this.draftMemberIds().includes(id);
  }

  protected memberById(id: string): FamilyMember | undefined {
    return this.members().find((m) => m.id === id);
  }

  protected async saveDrawer(): Promise<void> {
    const target = this.editingEvent();
    if (!target || this.savingDrawer()) return;

    this.savingDrawer.set(true);
    this.drawerError.set(null);
    try {
      const updated = await firstValueFrom(
        this.api.setEventMembers(target.id, this.draftMemberIds()));
      // Replace the event in the local cache so the agenda reflects the new
      // member chips without re-fetching everything.
      this.events.update((list) =>
        list.map((e) => (e.id === updated.id ? updated : e)));
      this.editingEvent.set(null);
    } catch {
      this.drawerError.set('No se pudo guardar. Inténtalo de nuevo.');
    } finally {
      this.savingDrawer.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    const now = new Date();
    const from = new Date(now);
    from.setDate(from.getDate() - this.lookbackDays);
    from.setHours(0, 0, 0, 0);
    const to = new Date(now);
    to.setDate(to.getDate() + this.lookaheadDays);
    to.setHours(0, 0, 0, 0);

    try {
      const [events, members] = await Promise.all([
        firstValueFrom(this.api.listEvents({ from, to })),
        firstValueFrom(this.membersApi.list()),
      ]);
      this.events.set(events);
      this.members.set(members);
    } catch {
      this.error.set('No se han podido cargar los eventos. Reintenta en un momento.');
    } finally {
      this.loading.set(false);
    }
  }

  private toLocalDate(iso: string): string {
    const d = new Date(iso);
    const year = d.getFullYear();
    const month = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  /**
   * Local YYYY-MM-DD for grouping. For all-day events the server already
   * computed the date in the family's timezone and shipped it as
   * `startDate` — using it verbatim avoids the UTC-vs-local shift that
   * caused Christmas to land on Dec 24 in America/New_York (issue #13).
   * Timed events fall through to the browser's local conversion, which
   * matches the time the user expects to see them at.
   */
  private eventLocalDate(event: CalendarEvent): string {
    if (event.isAllDay && event.startDate) {
      return event.startDate;
    }
    return this.toLocalDate(event.startAt);
  }

  /** Stable sort key: all-day events use their date string, timed events use the ISO instant. */
  private eventSortKey(event: CalendarEvent): string {
    if (event.isAllDay && event.startDate) {
      return event.startDate;
    }
    return event.startAt;
  }

  private dayLabel(date: string): string {
    const d = new Date(date + 'T00:00:00');
    return d.toLocaleDateString(this.locale, { weekday: 'long', day: 'numeric', month: 'short' });
  }
}
