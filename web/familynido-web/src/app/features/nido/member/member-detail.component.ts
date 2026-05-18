import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { CalendarService } from '../../../core/api/calendar.service';
import { FamilyMembersService } from '../../../core/api/family-members.service';
import { HouseholdTasksService } from '../../../core/api/household-tasks.service';
import { ScoresService } from '../../../core/api/scores.service';
import { AuthService } from '../../../core/auth/auth.service';
import { CalendarEvent } from '../../../core/models/calendar';
import { FamilyMember } from '../../../core/models/family-member';
import { HouseholdTask } from '../../../core/models/household-task';
import { MemberCompletion, MemberScore } from '../../../core/models/scores';
import { AvatarComponent } from '../../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { memberSubtitle } from '../member-formatting';
import { MemberAgendaSectionComponent } from './member-agenda-section.component';

/** Editable form state mirrored on a {@link FamilyMember}. */
interface EditForm {
  displayName: string;
  colorHex: string;
  birthDate: string;
  contactEmail: string;
}

/** Calendar events grouped by their YYYY-MM-DD bucket for the agenda list. */
interface DayEvents {
  date: string;
  label: string;
  events: CalendarEvent[];
}

/**
 * `/nido/:memberId` — detail view of a single family member. Aggregates the
 * items the rest of the app already attaches to a member (tasks they're
 * assigned to, calendar events from their linked Google calendars). For
 * admins, also acts as the management surface: edit, archive/restore and
 * delete the member.
 */
@Component({
  selector: 'fn-member-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent, IconComponent, FormsModule, RouterLink, MemberAgendaSectionComponent],
  templateUrl: './member-detail.component.html',
  styleUrl: './member-detail.component.css',
})
export class MemberDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly locale = inject(LOCALE_ID);
  private readonly membersApi = inject(FamilyMembersService);
  private readonly tasksApi = inject(HouseholdTasksService);
  private readonly calendarApi = inject(CalendarService);
  private readonly scoresApi = inject(ScoresService);
  protected readonly auth = inject(AuthService);

  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly member = signal<FamilyMember | null>(null);
  protected readonly tasks = signal<HouseholdTask[]>([]);
  protected readonly agenda = signal<DayEvents[]>([]);
  protected readonly score = signal<MemberScore | null>(null);
  protected readonly history = signal<MemberCompletion[]>([]);

  protected readonly editing = signal(false);
  protected readonly form = signal<EditForm>(this.emptyForm());
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  /** Cache-busting tag — bumped on photo upload/remove so the avatar refreshes. */
  protected readonly photoVersion = signal<string>('');

  protected readonly isAdmin = computed(() => this.auth.me()?.role === 'Admin');
  protected readonly isSelf = computed(() => this.auth.me()?.memberId === this.member()?.id);
  protected readonly canEditPhoto = computed(() => this.isAdmin() || this.isSelf());

  /** URL for `<fn-avatar [photoUrl]>`. Null when the member has no photo. */
  protected readonly memberPhotoUrl = computed(() => {
    const m = this.member();
    if (!m?.photoPath) return null;
    return this.membersApi.photoUrl(m.id, this.photoVersion());
  });

  protected readonly photoUploading = signal(false);
  protected readonly photoError = signal<string | null>(null);

  /** Tooltip surfaced on the photo-edit overlay button. */
  protected readonly photoUploadingTitle = $localize`:@@member-detail.photo.uploading-title:Subiendo…`;
  protected readonly changePhotoTitle = $localize`:@@member-detail.photo.change-title:Cambiar foto`;

  /** "últimas N · 🏆 P" line on top of the history card. */
  protected historySummary(): string {
    const n = this.history().length;
    const total = this.historyTotal();
    return $localize`:@@member-detail.history.summary:últimas ${n}:N: · 🏆 ${total}:POINTS:`;
  }

  protected readonly subtitle = computed(() => {
    const m = this.member();
    return m ? memberSubtitle(m) : '';
  });

  /** Tasks dropped into a stable order so the UI doesn't flicker on refresh. */
  protected readonly orderedTasks = computed(() =>
    [...this.tasks()].sort((a, b) => a.title.localeCompare(b.title)));

  /** History grouped by occurrence date (descending), one bucket per day. */
  protected readonly historyByDay = computed<{ date: string; label: string; rows: MemberCompletion[] }[]>(() => {
    const buckets = new Map<string, MemberCompletion[]>();
    for (const row of this.history()) {
      const arr = buckets.get(row.occurrenceDate);
      if (arr) arr.push(row); else buckets.set(row.occurrenceDate, [row]);
    }
    const formatter = new Intl.DateTimeFormat(this.locale, {
      weekday: 'long', day: 'numeric', month: 'long',
    });
    return [...buckets.entries()]
      .sort((a, b) => b[0].localeCompare(a[0]))
      .map(([date, rows]) => ({
        date,
        label: capitalize(formatter.format(new Date(date + 'T00:00:00'))),
        rows: rows.sort((a, b) => b.completedAt.localeCompare(a.completedAt)),
      }));
  });

  /** Sum of all points listed in the loaded history (used by the section header). */
  protected readonly historyTotal = computed(() =>
    this.history().reduce((sum, r) => sum + r.points, 0));

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('memberId');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    await this.loadAll(id);
  }

  // ─── data loading ────────────────────────────────────────────────────────

  private async loadAll(id: string): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.actionError.set(null);

    const now = new Date();
    const horizon = new Date(now.getTime());
    horizon.setDate(horizon.getDate() + 30);

    try {
      const [member, tasks, events] = await Promise.all([
        firstValueFrom(this.membersApi.get(id)),
        firstValueFrom(this.tasksApi.list({ memberId: id })),
        firstValueFrom(this.calendarApi.listEvents({ from: now, to: horizon, memberIds: [id] })),
      ]);
      this.member.set(member);
      // `list()` now paginates — the per-member dashboard shows a compact
      // view, so the default page (25 newest) is plenty. If a member ever
      // has more than 25 non-archived tasks we'll need to bump pageSize
      // here or add a "ver todas" link.
      this.tasks.set(tasks.items.filter((t) => !t.isArchived));
      this.agenda.set(this.groupByDay(events));
      // Score totals are best-effort; if the call fails we just hide the block.
      try {
        const score = await firstValueFrom(this.scoresApi.member(id));
        this.score.set(score);
      } catch {
        this.score.set(null);
      }
      // Completion history (latest 50). Best-effort like the score totals.
      try {
        const list = await firstValueFrom(this.scoresApi.history(id));
        this.history.set(list);
      } catch {
        this.history.set([]);
      }
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      if (status === 404) {
        this.notFound.set(true);
      } else {
        this.actionError.set($localize`:@@member-detail.error.load:No hemos podido cargar la información de este miembro.`);
      }
    } finally {
      this.loading.set(false);
    }
  }

  private groupByDay(events: CalendarEvent[]): DayEvents[] {
    const sorted = [...events].sort((a, b) => a.startAt.localeCompare(b.startAt));
    const buckets = new Map<string, CalendarEvent[]>();
    for (const ev of sorted) {
      // The "date bucket" is the local date of the event start. We use ISO YYYY-MM-DD
      // so the keys also sort as strings.
      const key = ev.startAt.slice(0, 10);
      const bucket = buckets.get(key);
      if (bucket) bucket.push(ev); else buckets.set(key, [ev]);
    }
    const formatter = new Intl.DateTimeFormat(this.locale, {
      weekday: 'long', day: 'numeric', month: 'long',
    });
    return [...buckets.entries()].map(([date, evs]) => ({
      date,
      label: capitalize(formatter.format(new Date(date + 'T00:00:00'))),
      events: evs,
    }));
  }

  // ─── edit ────────────────────────────────────────────────────────────────

  protected openEdit(): void {
    const m = this.member();
    if (!m) return;
    this.form.set({
      displayName: m.displayName,
      colorHex: m.colorHex,
      birthDate: m.birthDate ?? '',
      contactEmail: m.contactEmail ?? '',
    });
    this.formError.set(null);
    this.editing.set(true);
  }

  protected cancelEdit(): void {
    this.editing.set(false);
  }

  protected readForm<K extends keyof EditForm>(field: K, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.form.update((f) => ({ ...f, [field]: value as EditForm[K] }));
  }

  protected async submitEdit(): Promise<void> {
    if (this.submitting()) return;
    const id = this.member()?.id;
    if (!id) return;

    const f = this.form();
    if (f.displayName.trim().length === 0) {
      this.formError.set($localize`:@@nido.form.error.name-required:Necesitas un nombre.`);
      return;
    }
    if (!/^#[0-9a-fA-F]{6}$/.test(f.colorHex)) {
      this.formError.set($localize`:@@nido.form.error.color-format:El color debe tener formato #RRGGBB.`);
      return;
    }

    this.submitting.set(true);
    this.formError.set(null);
    try {
      const updated = await firstValueFrom(this.membersApi.update(id, {
        displayName: f.displayName.trim(),
        colorHex: f.colorHex,
        birthDate: f.birthDate || null,
        contactEmail: f.contactEmail.trim() || null,
      }));
      this.member.set({ ...this.member()!, ...updated });
      this.editing.set(false);
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      this.formError.set(status === 400
        ? $localize`:@@nido.form.error.invalid-fields:Algún campo no es válido. Revisa el formulario.`
        : $localize`:@@nido.form.error.save-unknown:No se pudo guardar. Inténtalo de nuevo.`);
    } finally {
      this.submitting.set(false);
    }
  }

  // ─── photo upload ─────────────────────────────────────────────────────────

  protected onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      void this.uploadPhoto(file);
    }
    // Reset the input so picking the same file twice still triggers change.
    input.value = '';
  }

  private async uploadPhoto(file: File): Promise<void> {
    const m = this.member();
    if (!m || this.photoUploading()) return;

    this.photoUploading.set(true);
    this.photoError.set(null);
    try {
      const updated = await firstValueFrom(this.membersApi.uploadPhoto(m.id, file));
      this.member.set({ ...m, ...updated });
      // New URL bytes — busts the browser cache.
      this.photoVersion.set(Date.now().toString());
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      this.photoError.set(status === 400
        ? $localize`:@@member-detail.photo.error.invalid:El archivo no es una imagen válida o supera el tamaño permitido.`
        : status === 403
          ? $localize`:@@member-detail.photo.error.forbidden:No tienes permiso para cambiar esta foto.`
          : $localize`:@@member-detail.photo.error.unknown:No se pudo subir la foto. Inténtalo de nuevo.`);
    } finally {
      this.photoUploading.set(false);
    }
  }

  protected async removePhoto(): Promise<void> {
    const m = this.member();
    if (!m || !m.photoPath || this.photoUploading()) return;
    if (!window.confirm($localize`:@@member-detail.photo.remove-confirm:¿Quitar la foto de perfil?`)) return;

    this.photoUploading.set(true);
    this.photoError.set(null);
    try {
      const updated = await firstValueFrom(this.membersApi.removePhoto(m.id));
      this.member.set({ ...m, ...updated });
      this.photoVersion.set(Date.now().toString());
    } catch {
      this.photoError.set($localize`:@@member-detail.photo.error.remove:No se pudo quitar la foto.`);
    } finally {
      this.photoUploading.set(false);
    }
  }

  // ─── archive / restore / delete ──────────────────────────────────────────

  protected async toggleActive(): Promise<void> {
    const m = this.member();
    if (!m) return;
    const archiveMsg = $localize`:@@member-detail.archive-confirm:¿Archivar a ${m.displayName}:NAME:? Quedará oculto en los selectores hasta que lo restaures.`;
    if (m.isActive && !window.confirm(archiveMsg)) {
      return;
    }
    this.actionError.set(null);
    try {
      const updated = m.isActive
        ? await firstValueFrom(this.membersApi.deactivate(m.id))
        : await firstValueFrom(this.membersApi.activate(m.id));
      this.member.set({ ...m, ...updated });
    } catch {
      this.actionError.set(m.isActive
        ? $localize`:@@member-detail.error.archive:No se pudo archivar el miembro.`
        : $localize`:@@member-detail.error.restore:No se pudo restaurar el miembro.`);
    }
  }

  protected async deleteMember(): Promise<void> {
    const m = this.member();
    if (!m) return;
    const deleteMsg = $localize`:@@member-detail.delete-confirm:¿Eliminar a ${m.displayName}:NAME: de forma permanente?\n\nEsta acción no se puede deshacer y solo conviene cuando el miembro fue creado por error. Si solo quieres ocultarlo, usa "Archivar".`;
    if (!window.confirm(deleteMsg)) return;

    this.actionError.set(null);
    try {
      await firstValueFrom(this.membersApi.delete(m.id));
      void this.router.navigateByUrl('/nido');
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      this.actionError.set(status === 409
        ? $localize`:@@member-detail.error.delete-pending-invitation:No se puede eliminar mientras tenga una invitación pendiente. Revócala antes desde el nido.`
        : $localize`:@@member-detail.error.delete:No se pudo eliminar el miembro.`);
    }
  }

  // ─── helpers ─────────────────────────────────────────────────────────────

  protected eventTime(ev: CalendarEvent): string {
    if (ev.isAllDay) return $localize`:@@member-detail.event.all-day:todo el día`;
    const d = new Date(ev.startAt);
    // `numeric` defers to the locale's hour cycle (issue #12).
    return d.toLocaleTimeString(this.locale, { hour: 'numeric', minute: '2-digit' });
  }

  protected recurrenceLabel(task: HouseholdTask): string {
    switch (task.recurrence) {
      case 'None':
        return task.dueDate
          ? $localize`:@@member-detail.recurrence.due-on:vence el ${task.dueDate}:DATE:`
          : $localize`:@@member-detail.recurrence.none:sin recurrencia`;
      case 'Daily':
        return $localize`:@@member-detail.recurrence.daily:cada día`;
      case 'Weekly':
        return task.weeklyDays && task.weeklyDays !== 'None'
          ? task.weeklyDays.toString()
          : $localize`:@@member-detail.recurrence.weekly:cada semana`;
      case 'Monthly':
        return task.monthlyDay === -1
          ? $localize`:@@member-detail.recurrence.monthly-last:último día del mes`
          : $localize`:@@member-detail.recurrence.monthly-day:día ${task.monthlyDay}:DAY: de cada mes`;
    }
  }

  private emptyForm(): EditForm {
    return { displayName: '', colorHex: '#C96442', birthDate: '', contactEmail: '' };
  }
}

function capitalize(s: string): string {
  return s.length === 0 ? s : s.charAt(0).toUpperCase() + s.slice(1);
}
