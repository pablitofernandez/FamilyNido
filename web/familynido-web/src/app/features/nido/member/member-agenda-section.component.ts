import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, computed, inject, input, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { MemberAgendaService } from '../../../core/api/member-agenda.service';
import { AuthService } from '../../../core/auth/auth.service';
import { buildTimeFormatter, reformatHourMinute } from '../../../core/locale/format-prefs';
import {
  AgendaDayOfWeek,
  AgendaTransportMode,
  MemberAgendaException,
  MemberAgendaExceptionInput,
  MemberAgendaPattern,
  MemberAgendaPatternInput,
} from '../../../core/models/agenda';
const WEEKDAY_ORDER: AgendaDayOfWeek[] = [
  'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday',
];

// Weekday + transport labels live inside the helper functions below so each
// branch goes through $localize and gets a stable trans-unit id.
function weekdayLabelFor(day: AgendaDayOfWeek): string {
  switch (day) {
    case 'Monday': return $localize`:@@member-agenda.weekday.monday:lunes`;
    case 'Tuesday': return $localize`:@@member-agenda.weekday.tuesday:martes`;
    case 'Wednesday': return $localize`:@@member-agenda.weekday.wednesday:miércoles`;
    case 'Thursday': return $localize`:@@member-agenda.weekday.thursday:jueves`;
    case 'Friday': return $localize`:@@member-agenda.weekday.friday:viernes`;
    case 'Saturday': return $localize`:@@member-agenda.weekday.saturday:sábado`;
    case 'Sunday': return $localize`:@@member-agenda.weekday.sunday:domingo`;
  }
}

function transportLabelFor(mode: AgendaTransportMode): string {
  switch (mode) {
    case 'None': return $localize`:@@member-agenda.transport.none:Sin transporte`;
    case 'Car': return $localize`:@@member-agenda.transport.car:Coche`;
    case 'Bus': return $localize`:@@member-agenda.transport.bus:Bus`;
    case 'Walk': return $localize`:@@member-agenda.transport.walk:A pie`;
    case 'Train': return $localize`:@@member-agenda.transport.train:Tren`;
    case 'Plane': return $localize`:@@member-agenda.transport.plane:Avión`;
    case 'Other': return $localize`:@@member-agenda.transport.other:Otro`;
  }
}

const TRANSPORT_ICONS: Record<AgendaTransportMode, string> = {
  None: '',
  Car: '🚗',
  Bus: '🚌',
  Walk: '🚶',
  Train: '🚆',
  Plane: '✈️',
  Other: '🧭',
};

/** Local form state mirroring a {@link MemberAgendaPatternInput} (strings + booleans for binding). */
interface PatternForm {
  id: string | null;
  dayOfWeek: AgendaDayOfWeek;
  label: string;
  location: string;
  startTime: string;
  endTime: string;
  transportMode: AgendaTransportMode;
  isAway: boolean;
  notes: string;
}

/** Local form state for {@link MemberAgendaExceptionInput}. */
interface ExceptionForm {
  id: string | null;
  date: string;
  label: string;
  location: string;
  startTime: string;
  endTime: string;
  transportMode: AgendaTransportMode;
  isAway: boolean;
  notes: string;
}

/**
 * Self-contained agenda block rendered inside the member-detail page. Loads
 * patterns + upcoming exceptions for one member and exposes inline edit/add/
 * delete forms. When `canEdit` is false the lists render read-only.
 */
@Component({
  selector: 'fn-member-agenda-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  templateUrl: './member-agenda-section.component.html',
  styleUrl: './member-agenda-section.component.css',
})
export class MemberAgendaSectionComponent implements OnInit {
  private readonly api = inject(MemberAgendaService);

  /** Member whose agenda is rendered. Required. */
  readonly memberId = input.required<string>();
  /** Whether the current user can mutate this agenda (admin or self). */
  readonly canEdit = input<boolean>(false);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly patterns = signal<MemberAgendaPattern[]>([]);
  protected readonly exceptions = signal<MemberAgendaException[]>([]);

  protected readonly weekdayOrder = WEEKDAY_ORDER;
  protected readonly transportModes: AgendaTransportMode[] = [
    'None', 'Car', 'Bus', 'Walk', 'Train', 'Plane', 'Other',
  ];

  /** Patterns grouped by weekday, ordered Mon→Sun, used for the read-only list. */
  protected readonly patternsByDay = computed(() => {
    const map = new Map<AgendaDayOfWeek, MemberAgendaPattern[]>();
    for (const day of WEEKDAY_ORDER) map.set(day, []);
    for (const p of this.patterns()) {
      const bucket = map.get(p.dayOfWeek);
      if (bucket) bucket.push(p);
    }
    for (const bucket of map.values()) {
      bucket.sort((a, b) => (a.startTime ?? '').localeCompare(b.startTime ?? ''));
    }
    return [...map.entries()].map(([day, list]) => ({ day, patterns: list }));
  });

  /** Upcoming exceptions sorted ascending by date, only the ad-hoc ones for now. */
  protected readonly upcomingExceptions = computed(() =>
    [...this.exceptions()]
      .filter((e) => !e.isCancelled || e.patternId !== null)
      .sort((a, b) => a.date.localeCompare(b.date)));

  // ─── form state ───────────────────────────────────────────────────────────

  protected readonly patternForm = signal<PatternForm | null>(null);
  protected readonly exceptionForm = signal<ExceptionForm | null>(null);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.refresh();
  }

  private async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    const today = new Date();
    const horizon = new Date(today);
    horizon.setDate(horizon.getDate() + 60);
    const fromIso = MemberAgendaSectionComponent.toIso(today);
    const toIso = MemberAgendaSectionComponent.toIso(horizon);

    try {
      const overview = await firstValueFrom(this.api.overview(fromIso, toIso));
      const id = this.memberId();
      this.patterns.set(overview.patterns.filter((p) => p.memberId === id));
      this.exceptions.set(overview.exceptions.filter((e) => e.memberId === id));
    } catch {
      this.error.set($localize`:@@member-agenda.error.load:No pudimos cargar la agenda.`);
    } finally {
      this.loading.set(false);
    }
  }

  // ─── formatting helpers ───────────────────────────────────────────────────

  /** Placeholder for empty exception labels. */
  protected readonly labelPlaceholder = $localize`:@@member-agenda.label-placeholder:(sin etiqueta)`;

  protected weekdayLabel(day: AgendaDayOfWeek): string {
    return weekdayLabelFor(day);
  }

  protected transportLabel(mode: AgendaTransportMode): string {
    return transportLabelFor(mode);
  }

  protected transportIcon(mode: AgendaTransportMode): string {
    return TRANSPORT_ICONS[mode];
  }

  protected timeLabel(start: string | null, end: string | null): string {
    const fmt = (t: string | null) => reformatHourMinute(t, this.timeFormatter);
    if (!start && !end) return $localize`:@@member-agenda.all-day:Todo el día`;
    if (start && !end) return `${fmt(start)} →`;
    if (!start && end) return `→ ${fmt(end)}`;
    return `${fmt(start)} – ${fmt(end)}`;
  }

  private readonly locale = inject(LOCALE_ID);
  private readonly auth = inject(AuthService);
  /** Display formatter that honours the user's `timeFormat` override. Issue #12. */
  private readonly timeFormatter = buildTimeFormatter(this.locale, this.auth.me()?.timeFormat);

  protected dateLabel(iso: string): string {
    const d = new Date(iso + 'T00:00:00');
    return d.toLocaleDateString(this.locale, { weekday: 'long', day: 'numeric', month: 'short' });
  }

  // ─── pattern form ─────────────────────────────────────────────────────────

  protected openCreatePattern(): void {
    this.patternForm.set({
      id: null,
      dayOfWeek: 'Tuesday',
      label: '',
      location: '',
      startTime: '',
      endTime: '',
      transportMode: 'Car',
      isAway: true,
      notes: '',
    });
    this.formError.set(null);
  }

  protected openEditPattern(p: MemberAgendaPattern): void {
    this.patternForm.set({
      id: p.id,
      dayOfWeek: p.dayOfWeek,
      label: p.label,
      location: p.location ?? '',
      startTime: (p.startTime ?? '').slice(0, 5),
      endTime: (p.endTime ?? '').slice(0, 5),
      transportMode: p.transportMode,
      isAway: p.isAway,
      notes: p.notes ?? '',
    });
    this.formError.set(null);
  }

  protected closePatternForm(): void {
    this.patternForm.set(null);
  }

  protected updatePatternField<K extends keyof PatternForm>(field: K, event: Event): void {
    const target = event.target as HTMLInputElement | HTMLSelectElement;
    const value = target.type === 'checkbox' ? (target as HTMLInputElement).checked : target.value;
    this.patternForm.update((f) => f ? { ...f, [field]: value as PatternForm[K] } : f);
  }

  protected async submitPattern(): Promise<void> {
    const form = this.patternForm();
    if (!form || this.saving()) return;
    if (form.label.trim().length === 0) {
      this.formError.set($localize`:@@member-agenda.form.error.name-required:Necesitas un nombre.`);
      return;
    }

    const body: MemberAgendaPatternInput = {
      memberId: this.memberId(),
      dayOfWeek: form.dayOfWeek,
      label: form.label.trim(),
      location: form.location.trim() === '' ? null : form.location.trim(),
      startTime: form.startTime === '' ? null : form.startTime,
      endTime: form.endTime === '' ? null : form.endTime,
      transportMode: form.transportMode,
      isAway: form.isAway,
      notes: form.notes.trim() === '' ? null : form.notes.trim(),
      isActive: true,
    };

    this.saving.set(true);
    this.formError.set(null);
    try {
      if (form.id) {
        await firstValueFrom(this.api.updatePattern(form.id, body));
      } else {
        await firstValueFrom(this.api.createPattern(body));
      }
      this.closePatternForm();
      await this.refresh();
    } catch {
      this.formError.set($localize`:@@member-agenda.form.error.save:No se pudo guardar.`);
    } finally {
      this.saving.set(false);
    }
  }

  protected async deletePattern(p: MemberAgendaPattern): Promise<void> {
    const msg = $localize`:@@member-agenda.delete-pattern-confirm:¿Eliminar el patrón "${p.label}:LABEL:" de los ${this.weekdayLabel(p.dayOfWeek)}:DAY:?`;
    if (!window.confirm(msg)) return;
    try {
      await firstValueFrom(this.api.deletePattern(p.id));
      await this.refresh();
    } catch {
      this.error.set($localize`:@@member-agenda.error.delete:No se pudo eliminar.`);
    }
  }

  // ─── exception form ──────────────────────────────────────────────────────

  protected openCreateException(): void {
    this.exceptionForm.set({
      id: null,
      date: MemberAgendaSectionComponent.toIso(new Date()),
      label: '',
      location: '',
      startTime: '',
      endTime: '',
      transportMode: 'Car',
      isAway: true,
      notes: '',
    });
    this.formError.set(null);
  }

  protected closeExceptionForm(): void {
    this.exceptionForm.set(null);
  }

  protected updateExceptionField<K extends keyof ExceptionForm>(field: K, event: Event): void {
    const target = event.target as HTMLInputElement | HTMLSelectElement;
    const value = target.type === 'checkbox' ? (target as HTMLInputElement).checked : target.value;
    this.exceptionForm.update((f) => f ? { ...f, [field]: value as ExceptionForm[K] } : f);
  }

  protected async submitException(): Promise<void> {
    const form = this.exceptionForm();
    if (!form || this.saving()) return;
    if (form.label.trim().length === 0) {
      this.formError.set($localize`:@@member-agenda.exception.error.name-required:Pon un nombre al día.`);
      return;
    }
    if (!form.date) {
      this.formError.set($localize`:@@member-agenda.exception.error.date-required:Falta la fecha.`);
      return;
    }

    const body: MemberAgendaExceptionInput = {
      memberId: this.memberId(),
      date: form.date,
      patternId: null, // ad-hoc (overrides come later, when added from a resolved entry)
      isCancelled: false,
      label: form.label.trim(),
      location: form.location.trim() === '' ? null : form.location.trim(),
      startTime: form.startTime === '' ? null : form.startTime,
      endTime: form.endTime === '' ? null : form.endTime,
      transportMode: form.transportMode,
      isAway: form.isAway,
      notes: form.notes.trim() === '' ? null : form.notes.trim(),
    };

    this.saving.set(true);
    this.formError.set(null);
    try {
      await firstValueFrom(this.api.createException(body));
      this.closeExceptionForm();
      await this.refresh();
    } catch {
      this.formError.set($localize`:@@member-agenda.form.error.save:No se pudo guardar.`);
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteException(e: MemberAgendaException): Promise<void> {
    const msg = $localize`:@@member-agenda.delete-exception-confirm:¿Eliminar la excepción del ${this.dateLabel(e.date)}:DATE:?`;
    if (!window.confirm(msg)) return;
    try {
      await firstValueFrom(this.api.deleteException(e.id));
      await this.refresh();
    } catch {
      this.error.set($localize`:@@member-agenda.error.delete:No se pudo eliminar.`);
    }
  }

  /** Format a Date as YYYY-MM-DD in local time. */
  private static toIso(date: Date): string {
    const y = date.getFullYear();
    const m = `${date.getMonth() + 1}`.padStart(2, '0');
    const d = `${date.getDate()}`.padStart(2, '0');
    return `${y}-${m}-${d}`;
  }
}
