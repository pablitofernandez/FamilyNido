import { DatePipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { FamilyMembersService } from '../../core/api/family-members.service';
import { SchoolService } from '../../core/api/school.service';
import { AuthService } from '../../core/auth/auth.service';
import { buildTimeFormatter, reformatHourMinute } from '../../core/locale/format-prefs';
import { FamilyMember } from '../../core/models/family-member';
import {
  Extracurricular,
  ExtracurricularRequest,
  HolidayRequest,
  ResolvedExtracurricular,
  ResolvedSchoolDay,
  SchoolDayScheduleSlot,
  SchoolHoliday,
  SchoolOverview,
  TransportMode,
} from '../../core/models/school';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

/** Editor target for the school-day pattern panel. */
interface DayPatternEditor {
  kidId: string;
  /** Map weekday → { dropoffMemberId, pickupMemberId } (empty string = no caretaker). */
  slotsByDay: Record<number, { dropoffMemberId: string; pickupMemberId: string }>;
}

/** Editor target for an extracurricular create/edit form. */
type ExtraEditorTarget = { mode: 'create' } | { mode: 'edit'; activity: Extracurricular };

const WEEKDAY_LABELS: Record<number, string> = {
  1: 'L', 2: 'M', 3: 'X', 4: 'J', 5: 'V', 6: 'S', 0: 'D',
};
const WEEKDAY_LONG: Record<number, string> = {
  1: 'lunes', 2: 'martes', 3: 'miércoles', 4: 'jueves', 5: 'viernes', 6: 'sábado', 0: 'domingo',
};
const WEEKDAY_FLAG_NAMES: Record<number, string> = {
  1: 'Monday', 2: 'Tuesday', 3: 'Wednesday', 4: 'Thursday', 5: 'Friday', 6: 'Saturday', 0: 'Sunday',
};
/** Reverse of WEEKDAY_FLAG_NAMES — server serialises DayOfWeek as the .NET name. */
const WEEKDAY_NAME_TO_NUMBER: Record<string, number> = {
  Sunday: 0, Monday: 1, Tuesday: 2, Wednesday: 3, Thursday: 4, Friday: 5, Saturday: 6,
};

const TRANSPORT_OPTIONS: { value: TransportMode; label: string; icon: string }[] = [
  { value: 'None', label: $localize`:@@school.transport.none:Sin transporte gestionado`, icon: '—' },
  { value: 'Bus', label: $localize`:@@school.transport.bus:Bus escolar`, icon: '🚌' },
  { value: 'Walk', label: $localize`:@@school.transport.walk:Andando`, icon: '🚶' },
  { value: 'Car', label: $localize`:@@school.transport.car:Coche`, icon: '🚗' },
];

/**
 * "El cole" — bus / walk / car logistics, extracurriculars and holidays in
 * one screen. Designed mobile-first: the week renders as one block per day
 * rather than a wide grid, so the household tablet at the entrance stays
 * readable and a phone can scroll through it naturally.
 */
@Component({
  selector: 'fn-school',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent, DatePipe, IconComponent, NgClass],
  templateUrl: './school.component.html',
  styleUrl: './school.component.css',
})
export class SchoolComponent implements OnInit {
  private readonly api = inject(SchoolService);
  private readonly membersApi = inject(FamilyMembersService);

  protected readonly transportOptions = TRANSPORT_OPTIONS;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly members = signal<FamilyMember[]>([]);
  protected readonly overview = signal<SchoolOverview | null>(null);

  /** ISO date of the Monday currently shown in the weekly grid. */
  protected readonly weekStart = signal(monday(new Date()));

  /** Cached array of the 7 dates in the current week. */
  protected readonly weekDays = computed(() => {
    const start = new Date(this.weekStart());
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      return iso(d);
    });
  });

  /** Members with type Child (for the "kids" sections). */
  protected readonly kids = computed(() =>
    this.members().filter((m) => m.isActive && m.memberType === 'Child'));

  /** All active members eligible to be caretakers (Adults + Other). */
  protected readonly caretakers = computed(() =>
    this.members().filter((m) => m.isActive && (m.memberType === 'Adult' || m.memberType === 'Other')));

  /** Map kidId → cached TransportMode (loaded together with the profile editor). */
  private readonly kidTransportMode = signal<Record<string, TransportMode>>({});

  // ─── day pattern editor ─────────────────────────────────────────────────
  protected readonly dayPatternEditor = signal<DayPatternEditor | null>(null);
  protected readonly savingPattern = signal(false);

  // ─── per-day exception editor ────────────────────────────────────────────
  protected readonly dayExceptionEditor = signal<{ kidId: string; date: string } | null>(null);
  protected readonly dayExceptionDropoff = signal<string>('');
  protected readonly dayExceptionPickup = signal<string>('');
  protected readonly dayExceptionMorning = signal<string>('');
  protected readonly dayExceptionAfternoon = signal<string>('');
  protected readonly dayExceptionCancel = signal(false);
  protected readonly dayExceptionNotes = signal('');
  protected readonly savingDayException = signal(false);

  // ─── extracurricular editor ──────────────────────────────────────────────
  protected readonly extraEditor = signal<ExtraEditorTarget | null>(null);
  protected readonly extraName = signal('');
  protected readonly extraMemberId = signal('');
  protected readonly extraLocation = signal('');
  protected readonly extraPhone = signal('');
  protected readonly extraDays = signal<Set<number>>(new Set());
  protected readonly extraStartTime = signal('17:00');
  protected readonly extraEndTime = signal('18:00');
  protected readonly extraStartDate = signal(iso(new Date()));
  protected readonly extraEndDate = signal('');
  protected readonly extraDropoff = signal('');
  protected readonly extraPickup = signal('');
  protected readonly extraNotes = signal('');
  protected readonly savingExtra = signal(false);

  // ─── extracurricular per-date exception editor ──────────────────────────
  protected readonly extraExceptionEditor = signal<{ extraId: string; date: string } | null>(null);
  protected readonly extraExceptionCancel = signal(false);
  protected readonly extraExceptionDropoff = signal('');
  protected readonly extraExceptionPickup = signal('');
  protected readonly extraExceptionNotes = signal('');
  protected readonly savingExtraException = signal(false);

  // ─── holiday editor ──────────────────────────────────────────────────────
  protected readonly holidayEditor = signal<{ id: string | null } | null>(null);
  protected readonly holidayLabel = signal('');
  protected readonly holidayStart = signal('');
  protected readonly holidayEnd = signal('');
  protected readonly savingHoliday = signal(false);

  // ─── school profile editor ───────────────────────────────────────────────
  protected readonly profileEditor = signal<{ memberId: string } | null>(null);
  protected readonly profileSchool = signal('');
  protected readonly profileGrade = signal('');
  protected readonly profileTutor = signal('');
  protected readonly profileTransport = signal<TransportMode>('None');
  protected readonly profileMorningTime = signal('');
  protected readonly profileAfternoonTime = signal('');
  protected readonly profileNotes = signal('');
  protected readonly savingProfile = signal(false);

  ngOnInit(): void {
    void this.loadAll();
  }

  // ─── loading ─────────────────────────────────────────────────────────────

  private async loadAll(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [members, overview] = await Promise.all([
        firstValueFrom(this.membersApi.list()),
        firstValueFrom(this.api.overview(this.weekDays()[0], this.weekDays()[6])),
      ]);
      this.members.set(members);
      this.overview.set(overview);
      // Pre-load each kid's transport mode so the badge in the kids list shows
      // the right icon without forcing the user to open the profile editor.
      await this.loadTransportModes(members.filter((m) => m.isActive && m.memberType === 'Child'));
    } catch {
      this.error.set($localize`:@@school.error.load:No se pudo cargar el módulo del cole.`);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadTransportModes(kids: FamilyMember[]): Promise<void> {
    const updates: Record<string, TransportMode> = {};
    for (const kid of kids) {
      try {
        const profile = await firstValueFrom(this.api.getProfile(kid.id));
        updates[kid.id] = profile?.transportMode ?? 'None';
      } catch {
        updates[kid.id] = 'None';
      }
    }
    this.kidTransportMode.update((map) => ({ ...map, ...updates }));
  }

  protected async previousWeek(): Promise<void> {
    this.weekStart.set(addDays(this.weekStart(), -7));
    await this.refreshOverview();
  }

  protected async nextWeek(): Promise<void> {
    this.weekStart.set(addDays(this.weekStart(), 7));
    await this.refreshOverview();
  }

  protected async thisWeek(): Promise<void> {
    this.weekStart.set(monday(new Date()));
    await this.refreshOverview();
  }

  private async refreshOverview(): Promise<void> {
    try {
      const overview = await firstValueFrom(this.api.overview(this.weekDays()[0], this.weekDays()[6]));
      this.overview.set(overview);
    } catch {
      this.error.set($localize`:@@school.error.refresh-week:No se pudo refrescar la semana.`);
    }
  }

  // ─── display helpers ─────────────────────────────────────────────────────

  protected memberName(memberId: string | null): string {
    if (!memberId) return '—';
    return this.members().find((m) => m.id === memberId)?.displayName ?? '—';
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

  private readonly locale = inject(LOCALE_ID);
  private readonly auth = inject(AuthService);
  /** Display formatter for "HH:MM" → "9:00 AM" / "09:00" honouring user pref. Issue #12. */
  private readonly timeFormatter = buildTimeFormatter(this.locale, this.auth.me()?.timeFormat);

  protected dayLabel(iso: string): string {
    return new Date(iso + 'T00:00:00').toLocaleDateString(this.locale, { weekday: 'long', day: 'numeric', month: 'short' });
  }

  protected holidayFor(date: string): SchoolHoliday | null {
    const overview = this.overview();
    if (!overview) return null;
    return overview.holidays.find((h) => h.startDate <= date && h.endDate >= date) ?? null;
  }

  /** School-day rows for a given date. */
  protected daysFor(date: string): ResolvedSchoolDay[] {
    return this.overview()?.resolvedDays.filter((b) => b.date === date) ?? [];
  }

  /** Extracurricular sessions for a given date. */
  protected extrasFor(date: string): ResolvedExtracurricular[] {
    return (this.overview()?.resolvedExtracurriculars ?? [])
      .filter((e) => e.date === date)
      .slice()
      .sort((a, b) => a.startTime.localeCompare(b.startTime));
  }

  protected formatTime(value: string): string {
    return reformatHourMinute(value, this.timeFormatter);
  }

  protected weekdayShort(d: number): string {
    return WEEKDAY_LABELS[d];
  }

  /** Single-character icon for the kid's transport mode. */
  protected transportIcon(kidId: string): string {
    const mode = this.kidTransportMode()[kidId] ?? 'None';
    return TRANSPORT_OPTIONS.find((o) => o.value === mode)?.icon ?? '🎒';
  }

  protected transportLabel(kidId: string): string {
    const mode = this.kidTransportMode()[kidId] ?? 'None';
    return TRANSPORT_OPTIONS.find((o) => o.value === mode)?.label ?? '—';
  }

  // ─── day pattern (weekly schedule per kid) ──────────────────────────────

  protected openDayPatternEditor(kidId: string): void {
    const overview = this.overview();
    const existing = overview?.schedule.find((s) => s.memberId === kidId)?.slots ?? [];
    const slotsByDay: Record<number, { dropoffMemberId: string; pickupMemberId: string }> = {};
    for (const slot of existing) {
      // The API serialises DayOfWeek as its .NET name ("Monday", "Tuesday"…)
      // through JsonStringEnumConverter — normalise to the numeric key we use
      // everywhere else in this component before stashing the entry.
      const dayNumber = parseDayOfWeek(slot.dayOfWeek);
      slotsByDay[dayNumber] = {
        dropoffMemberId: slot.dropoffMemberId ?? '',
        pickupMemberId: slot.pickupMemberId ?? '',
      };
    }
    this.dayPatternEditor.set({ kidId, slotsByDay });
  }

  protected closeDayPatternEditor(): void {
    this.dayPatternEditor.set(null);
  }

  protected onDayPatternChange(day: number, field: 'dropoff' | 'pickup', value: string): void {
    const editor = this.dayPatternEditor();
    if (!editor) return;
    const current = editor.slotsByDay[day] ?? { dropoffMemberId: '', pickupMemberId: '' };
    const next = field === 'dropoff'
      ? { ...current, dropoffMemberId: value }
      : { ...current, pickupMemberId: value };
    this.dayPatternEditor.set({
      ...editor,
      slotsByDay: { ...editor.slotsByDay, [day]: next },
    });
  }

  protected dayPatternValueFor(day: number, field: 'dropoff' | 'pickup'): string {
    const editor = this.dayPatternEditor();
    if (!editor) return '';
    const slot = editor.slotsByDay[day];
    if (!slot) return '';
    return field === 'dropoff' ? slot.dropoffMemberId : slot.pickupMemberId;
  }

  /** Whether the drop-off column should be visible for this kid (Walk/Car/None — bus only fills pickup). */
  protected showDropoffColumn(kidId: string | null): boolean {
    if (!kidId) return true;
    const mode = this.kidTransportMode()[kidId] ?? 'None';
    return mode !== 'Bus';
  }

  protected async saveDayPattern(): Promise<void> {
    const editor = this.dayPatternEditor();
    if (!editor || this.savingPattern()) return;
    this.savingPattern.set(true);
    this.error.set(null);
    try {
      const slots: SchoolDayScheduleSlot[] = Object.entries(editor.slotsByDay)
        .map(([day, slot]) => ({
          dayOfWeek: Number(day),
          dropoffMemberId: slot.dropoffMemberId || null,
          pickupMemberId: slot.pickupMemberId || null,
        }))
        .filter((s) => s.dropoffMemberId !== null || s.pickupMemberId !== null);

      await firstValueFrom(this.api.replaceDaySchedule(editor.kidId, { slots }));
      this.dayPatternEditor.set(null);
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.save-pattern:No se pudo guardar el patrón.`);
    } finally {
      this.savingPattern.set(false);
    }
  }

  // ─── per-day exception ───────────────────────────────────────────────────

  protected openDayExceptionEditor(kidId: string, date: string): void {
    const existing = this.overview()?.dayExceptions.find((e) => e.memberId === kidId && e.date === date);
    this.dayExceptionEditor.set({ kidId, date });
    this.dayExceptionCancel.set(existing?.isCancelled ?? false);
    this.dayExceptionDropoff.set(existing?.dropoffMemberId ?? '');
    this.dayExceptionPickup.set(existing?.pickupMemberId ?? '');
    this.dayExceptionMorning.set(existing?.morningTime?.slice(0, 5) ?? '');
    this.dayExceptionAfternoon.set(existing?.afternoonTime?.slice(0, 5) ?? '');
    this.dayExceptionNotes.set(existing?.notes ?? '');
  }

  protected closeDayExceptionEditor(): void {
    this.dayExceptionEditor.set(null);
  }

  protected async saveDayException(): Promise<void> {
    const target = this.dayExceptionEditor();
    if (!target || this.savingDayException()) return;
    this.savingDayException.set(true);
    this.error.set(null);
    try {
      const isCancelled = this.dayExceptionCancel();
      const dropoff = this.dayExceptionDropoff();
      const pickup = this.dayExceptionPickup();
      const morning = this.dayExceptionMorning();
      const afternoon = this.dayExceptionAfternoon();
      if (!isCancelled
          && !dropoff
          && !pickup
          && !morning
          && !afternoon
          && this.dayExceptionNotes().trim().length === 0) {
        this.error.set($localize`:@@school.error.exception-empty:Marca el día como cancelado o cambia al menos un responsable, una hora o añade una nota.`);
        return;
      }
      await firstValueFrom(this.api.setDayException(target.kidId, target.date, {
        isCancelled,
        dropoffMemberId: isCancelled ? null : (dropoff || null),
        pickupMemberId: isCancelled ? null : (pickup || null),
        morningTime: isCancelled || !morning ? null : ensureSeconds(morning),
        afternoonTime: isCancelled || !afternoon ? null : ensureSeconds(afternoon),
        notes: this.dayExceptionNotes().trim() || null,
      }));
      this.dayExceptionEditor.set(null);
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.save-day-exception:No se pudo guardar la excepción.`);
    } finally {
      this.savingDayException.set(false);
    }
  }

  protected async clearDayException(): Promise<void> {
    const target = this.dayExceptionEditor();
    if (!target || this.savingDayException()) return;
    this.savingDayException.set(true);
    try {
      await firstValueFrom(this.api.removeDayException(target.kidId, target.date));
      this.dayExceptionEditor.set(null);
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.clear-day-exception:No se pudo quitar la excepción.`);
    } finally {
      this.savingDayException.set(false);
    }
  }

  // ─── holiday CRUD ────────────────────────────────────────────────────────

  protected openHolidayEditor(holiday: SchoolHoliday | null): void {
    this.holidayEditor.set({ id: holiday?.id ?? null });
    this.holidayLabel.set(holiday?.label ?? '');
    this.holidayStart.set(holiday?.startDate ?? this.weekDays()[0]);
    this.holidayEnd.set(holiday?.endDate ?? this.weekDays()[0]);
  }

  protected closeHolidayEditor(): void {
    this.holidayEditor.set(null);
  }

  protected async saveHoliday(): Promise<void> {
    const editor = this.holidayEditor();
    if (!editor || this.savingHoliday()) return;
    if (this.holidayLabel().trim().length === 0) {
      this.error.set($localize`:@@school.error.holiday-label-required:Falta la etiqueta del festivo.`);
      return;
    }
    this.savingHoliday.set(true);
    this.error.set(null);
    try {
      const body: HolidayRequest = {
        label: this.holidayLabel().trim(),
        startDate: this.holidayStart(),
        endDate: this.holidayEnd(),
      };
      if (editor.id) {
        await firstValueFrom(this.api.updateHoliday(editor.id, body));
      } else {
        await firstValueFrom(this.api.addHoliday(body));
      }
      this.holidayEditor.set(null);
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.save-holiday:No se pudo guardar el festivo.`);
    } finally {
      this.savingHoliday.set(false);
    }
  }

  protected async deleteHoliday(holiday: SchoolHoliday): Promise<void> {
    if (!window.confirm($localize`:@@school.holiday-delete-confirm:¿Borrar el festivo "${holiday.label}:LABEL:"?`)) return;
    try {
      await firstValueFrom(this.api.deleteHoliday(holiday.id));
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.delete-holiday:No se pudo borrar el festivo.`);
    }
  }

  // ─── extracurricular CRUD ───────────────────────────────────────────────

  protected openExtraEditor(target: ExtraEditorTarget): void {
    this.extraEditor.set(target);
    if (target.mode === 'edit') {
      const a = target.activity;
      this.extraName.set(a.name);
      this.extraMemberId.set(a.memberId);
      this.extraLocation.set(a.location ?? '');
      this.extraPhone.set(a.contactPhone ?? '');
      this.extraDays.set(parseDayMask(a.weeklyDays));
      this.extraStartTime.set(a.startTime.slice(0, 5));
      this.extraEndTime.set(a.endTime.slice(0, 5));
      this.extraStartDate.set(a.startDate);
      this.extraEndDate.set(a.endDate ?? '');
      this.extraDropoff.set(a.defaultDropoffMemberId ?? '');
      this.extraPickup.set(a.defaultPickupMemberId ?? '');
      this.extraNotes.set(a.notes ?? '');
    } else {
      const firstKid = this.kids()[0]?.id ?? '';
      this.extraName.set('');
      this.extraMemberId.set(firstKid);
      this.extraLocation.set('');
      this.extraPhone.set('');
      this.extraDays.set(new Set());
      this.extraStartTime.set('17:00');
      this.extraEndTime.set('18:00');
      this.extraStartDate.set(iso(new Date()));
      this.extraEndDate.set('');
      this.extraDropoff.set('');
      this.extraPickup.set('');
      this.extraNotes.set('');
    }
  }

  protected closeExtraEditor(): void {
    this.extraEditor.set(null);
  }

  protected toggleExtraDay(day: number): void {
    const next = new Set(this.extraDays());
    if (next.has(day)) next.delete(day); else next.add(day);
    this.extraDays.set(next);
  }

  protected isExtraDaySelected(day: number): boolean {
    return this.extraDays().has(day);
  }

  protected async saveExtra(): Promise<void> {
    const target = this.extraEditor();
    if (!target || this.savingExtra()) return;

    if (this.extraName().trim().length === 0 || !this.extraMemberId()) {
      this.error.set($localize`:@@school.error.extra-name-or-kid:Falta el nombre o el niño.`);
      return;
    }
    if (this.extraDays().size === 0) {
      this.error.set($localize`:@@school.error.extra-no-days:Selecciona al menos un día de la semana.`);
      return;
    }

    this.savingExtra.set(true);
    this.error.set(null);
    try {
      const body: ExtracurricularRequest = {
        memberId: this.extraMemberId(),
        name: this.extraName().trim(),
        location: this.extraLocation().trim() || null,
        contactPhone: this.extraPhone().trim() || null,
        weeklyDays: serialiseDayMask(this.extraDays()),
        startTime: ensureSeconds(this.extraStartTime()),
        endTime: ensureSeconds(this.extraEndTime()),
        startDate: this.extraStartDate(),
        endDate: this.extraEndDate() || null,
        defaultDropoffMemberId: this.extraDropoff() || null,
        defaultPickupMemberId: this.extraPickup() || null,
        notes: this.extraNotes().trim() || null,
      };
      if (target.mode === 'edit') {
        await firstValueFrom(this.api.updateExtracurricular(target.activity.id, body));
      } else {
        await firstValueFrom(this.api.addExtracurricular(body));
      }
      this.extraEditor.set(null);
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.save-extra:No se pudo guardar la extraescolar.`);
    } finally {
      this.savingExtra.set(false);
    }
  }

  protected async archiveExtra(activity: Extracurricular): Promise<void> {
    try {
      await firstValueFrom(this.api.archiveExtracurricular(activity.id));
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.archive-extra:No se pudo archivar.`);
    }
  }

  // ─── extracurricular per-day exception ──────────────────────────────────

  protected openExtraExceptionEditor(extraId: string, date: string): void {
    const existing = this.overview()?.extracurricularExceptions
      .find((e) => e.extracurricularId === extraId && e.date === date);
    this.extraExceptionEditor.set({ extraId, date });
    this.extraExceptionCancel.set(existing?.isCancelled ?? false);
    this.extraExceptionDropoff.set(existing?.dropoffMemberId ?? '');
    this.extraExceptionPickup.set(existing?.pickupMemberId ?? '');
    this.extraExceptionNotes.set(existing?.notes ?? '');
  }

  protected closeExtraExceptionEditor(): void {
    this.extraExceptionEditor.set(null);
  }

  protected async saveExtraException(): Promise<void> {
    const target = this.extraExceptionEditor();
    if (!target || this.savingExtraException()) return;
    this.savingExtraException.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.setExtracurricularException(target.extraId, target.date, {
        isCancelled: this.extraExceptionCancel(),
        dropoffMemberId: this.extraExceptionCancel() ? null : (this.extraExceptionDropoff() || null),
        pickupMemberId: this.extraExceptionCancel() ? null : (this.extraExceptionPickup() || null),
        notes: this.extraExceptionNotes().trim() || null,
      }));
      this.extraExceptionEditor.set(null);
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.save-extra-exception:No se pudo guardar la excepción.`);
    } finally {
      this.savingExtraException.set(false);
    }
  }

  protected async clearExtraException(): Promise<void> {
    const target = this.extraExceptionEditor();
    if (!target || this.savingExtraException()) return;
    this.savingExtraException.set(true);
    try {
      await firstValueFrom(this.api.removeExtracurricularException(target.extraId, target.date));
      this.extraExceptionEditor.set(null);
      await this.refreshOverview();
    } catch {
      this.error.set($localize`:@@school.error.clear-extra-exception:No se pudo quitar la excepción.`);
    } finally {
      this.savingExtraException.set(false);
    }
  }

  // ─── school profile (per kid) ───────────────────────────────────────────

  protected async openProfileEditor(memberId: string): Promise<void> {
    this.profileEditor.set({ memberId });
    try {
      const profile = await firstValueFrom(this.api.getProfile(memberId));
      this.profileSchool.set(profile?.schoolName ?? '');
      this.profileGrade.set(profile?.grade ?? '');
      this.profileTutor.set(profile?.tutor ?? '');
      this.profileTransport.set(profile?.transportMode ?? 'None');
      this.profileMorningTime.set(profile?.morningTime?.slice(0, 5) ?? '');
      this.profileAfternoonTime.set(profile?.afternoonTime?.slice(0, 5) ?? '');
      this.profileNotes.set(profile?.notes ?? '');
      this.kidTransportMode.update((map) => ({ ...map, [memberId]: profile?.transportMode ?? 'None' }));
    } catch {
      this.profileSchool.set('');
      this.profileGrade.set('');
      this.profileTutor.set('');
      this.profileTransport.set('None');
      this.profileMorningTime.set('');
      this.profileAfternoonTime.set('');
      this.profileNotes.set('');
    }
  }

  protected closeProfileEditor(): void {
    this.profileEditor.set(null);
  }

  protected async saveProfile(): Promise<void> {
    const target = this.profileEditor();
    if (!target || this.savingProfile()) return;
    this.savingProfile.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.upsertProfile(target.memberId, {
        schoolName: this.profileSchool().trim() || null,
        grade: this.profileGrade().trim() || null,
        tutor: this.profileTutor().trim() || null,
        transportMode: this.profileTransport(),
        morningTime: this.profileMorningTime() ? ensureSeconds(this.profileMorningTime()) : null,
        afternoonTime: this.profileAfternoonTime() ? ensureSeconds(this.profileAfternoonTime()) : null,
        notes: this.profileNotes().trim() || null,
      }));
      this.kidTransportMode.update((map) => ({ ...map, [target.memberId]: this.profileTransport() }));
      this.profileEditor.set(null);
    } catch {
      this.error.set($localize`:@@school.error.save-profile:No se pudo guardar la ficha.`);
    } finally {
      this.savingProfile.set(false);
    }
  }

  // ─── input handlers (typed shortcuts) ────────────────────────────────────

  protected onText(setter: (v: string) => void) {
    return (event: Event) => setter((event.target as HTMLInputElement).value);
  }

  // ─── extracurriculars listing helpers ────────────────────────────────────

  protected extrasByKid(kidId: string): Extracurricular[] {
    return (this.overview()?.extracurriculars ?? [])
      .filter((e) => e.memberId === kidId)
      .slice()
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  protected describeExtra(activity: Extracurricular): string {
    const days = parseDayMask(activity.weeklyDays);
    const dayLabels = [...days].sort().map((d) => WEEKDAY_LONG[d]).join(', ');
    return `${dayLabels} · ${this.formatTime(activity.startTime)}–${this.formatTime(activity.endTime)}`;
  }

  // ─── i18n string builders (lifted from the template) ─────────────────────

  /**
   * Renders one school-day row as HTML so the strong/em tags survive translation
   * in a single trans-unit per branch (kid · drop-off · pickup ·…). Mirrors the
   * pattern used in dashboard.schoolDayRowHtml.
   */
  protected schoolDayRowHtml(b: ResolvedSchoolDay): string {
    const kid = `<strong>${this.escape(this.memberName(b.memberId))}</strong>`;
    const morning = b.morningTime ? this.formatTime(b.morningTime) : '';
    const afternoon = b.afternoonTime ? this.formatTime(b.afternoonTime) : '';

    if (!b.dropoffMemberId && !b.pickupMemberId) {
      return afternoon
        ? $localize`:@@school.row.unassigned-time:${kid}:KID: · <em>sin asignar</em> · a las ${afternoon}:TIME:`
        : $localize`:@@school.row.unassigned:${kid}:KID: · <em>sin asignar</em>`;
    }

    let html = kid;
    if (b.dropoffMemberId) {
      const drop = `<strong>${this.escape(this.memberName(b.dropoffMemberId))}</strong>`;
      html += morning
        ? $localize`:@@school.row.drop-time: · lleva ${drop}:NAME: a las ${morning}:TIME:`
        : $localize`:@@school.row.drop: · lleva ${drop}:NAME:`;
    }
    if (b.pickupMemberId) {
      const pick = `<strong>${this.escape(this.memberName(b.pickupMemberId))}</strong>`;
      html += afternoon
        ? $localize`:@@school.row.pickup-time: · recoge ${pick}:NAME: a las ${afternoon}:TIME:`
        : $localize`:@@school.row.pickup: · recoge ${pick}:NAME:`;
    }
    return html;
  }

  /** "Lleva X · recoge Y · nota Z" line under an extracurricular row. */
  protected extraTransportLine(e: ResolvedExtracurricular): string {
    const drop = e.dropoffMemberId ? this.escape(this.memberName(e.dropoffMemberId)) : '—';
    const pick = e.pickupMemberId ? this.escape(this.memberName(e.pickupMemberId)) : '—';
    let html = $localize`:@@school.extra.row:Lleva ${drop}:DROP: · recoge ${pick}:PICK:`;
    if (e.notes) {
      html += ` · ${this.escape(e.notes)}`;
    }
    return html;
  }

  /** Defaults line under an extracurricular card (Lleva X · recoge Y · 📞 …). */
  protected extraDefaultsLine(a: Extracurricular): string {
    const drop = a.defaultDropoffMemberId ? this.escape(this.memberName(a.defaultDropoffMemberId)) : '—';
    const pick = a.defaultPickupMemberId ? this.escape(this.memberName(a.defaultPickupMemberId)) : '—';
    let html = $localize`:@@school.extra.defaults:Lleva ${drop}:DROP: · recoge ${pick}:PICK:`;
    if (a.contactPhone) {
      html += ` · 📞 ${this.escape(a.contactPhone)}`;
    }
    return html;
  }

  /** Modal headings that include a member name. */
  protected dayPatternHeading(kidId: string): string {
    return $localize`:@@school.day-pattern.heading:Día a día — ${this.memberName(kidId)}:NAME:`;
  }
  protected profileHeading(memberId: string): string {
    return $localize`:@@school.profile-form.heading:Ficha — ${this.memberName(memberId)}:NAME:`;
  }

  private escape(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
}

// ─── helpers ──────────────────────────────────────────────────────────────

function iso(d: Date): string {
  const y = d.getFullYear();
  const m = `${d.getMonth() + 1}`.padStart(2, '0');
  const day = `${d.getDate()}`.padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function monday(d: Date): string {
  const cursor = new Date(d);
  cursor.setHours(0, 0, 0, 0);
  const diff = (cursor.getDay() + 6) % 7;
  cursor.setDate(cursor.getDate() - diff);
  return iso(cursor);
}

function addDays(isoDate: string, delta: number): string {
  const d = new Date(isoDate + 'T00:00:00');
  d.setDate(d.getDate() + delta);
  return iso(d);
}

function parseDayMask(value: string): Set<number> {
  const out = new Set<number>();
  if (!value) return out;
  for (const token of value.split(',').map((s) => s.trim())) {
    for (const [key, name] of Object.entries(WEEKDAY_FLAG_NAMES)) {
      if (name === token) out.add(Number(key));
    }
  }
  return out;
}

function serialiseDayMask(days: Set<number>): string {
  return [...days]
    .sort()
    .map((d) => WEEKDAY_FLAG_NAMES[d])
    .join(', ');
}

function ensureSeconds(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

/** Map the .NET DayOfWeek name received over the wire back to its numeric ordinal. */
function parseDayOfWeek(value: number | string): number {
  if (typeof value === 'number') return value;
  return WEEKDAY_NAME_TO_NUMBER[value] ?? 0;
}
