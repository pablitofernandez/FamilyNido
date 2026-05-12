import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { FamilyMembersService } from '../../core/api/family-members.service';
import { HealthService } from '../../core/api/health.service';
import { FamilyMember } from '../../core/models/family-member';
import { Medication, MedicationRequest, MemberHealth, UpsertHealthProfileRequest, Vaccination, VaccinationRequest } from '../../core/models/health';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

type Tab = 'profile' | 'vaccinations' | 'medications';

/**
 * "Salud" — health card per member with three sub-views: ficha (profile),
 * vacunas (vaccinations) and medicaciones (medications). Mirrors the structure
 * of /tasks in tone (member selector → tab content → inline editor) so the
 * navigation feels native to the rest of the app.
 */
@Component({
  selector: 'fn-health',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent, DatePipe, IconComponent],
  templateUrl: './health.component.html',
  styleUrl: './health.component.css',
})
export class HealthComponent implements OnInit {
  private readonly membersApi = inject(FamilyMembersService);
  private readonly api = inject(HealthService);

  protected readonly members = signal<FamilyMember[]>([]);
  protected readonly selectedMemberId = signal<string | null>(null);
  protected readonly health = signal<MemberHealth | null>(null);
  protected readonly tab = signal<Tab>('profile');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // ─── profile editing ──────────────────────────────────────────────────────
  protected readonly profileEditing = signal(false);
  protected readonly profileBloodType = signal('');
  protected readonly profileAllergies = signal('');
  protected readonly profileConditions = signal('');
  protected readonly profileNotes = signal('');
  protected readonly profileSaving = signal(false);

  // ─── vaccination editor ───────────────────────────────────────────────────
  protected readonly vaccinationEditorOpen = signal(false);
  protected readonly vaccinationEditingId = signal<string | null>(null);
  protected readonly vaccName = signal('');
  protected readonly vaccDate = signal('');
  protected readonly vaccNextDate = signal('');
  protected readonly vaccNotes = signal('');
  protected readonly vaccSaving = signal(false);

  // ─── medication editor ────────────────────────────────────────────────────
  protected readonly medicationEditorOpen = signal(false);
  protected readonly medicationEditingId = signal<string | null>(null);
  protected readonly medName = signal('');
  protected readonly medDose = signal('');
  protected readonly medFreq = signal('');
  protected readonly medStart = signal('');
  protected readonly medEnd = signal('');
  protected readonly medInstructions = signal('');
  protected readonly medSaving = signal(false);

  protected readonly selectedMember = computed(() =>
    this.members().find((m) => m.id === this.selectedMemberId()) ?? null);

  ngOnInit(): void {
    void this.loadMembers();
  }

  // ─── member selection ─────────────────────────────────────────────────────

  protected async pickMember(memberId: string): Promise<void> {
    if (this.selectedMemberId() === memberId) return;
    this.selectedMemberId.set(memberId);
    this.tab.set('profile');
    this.profileEditing.set(false);
    this.vaccinationEditorOpen.set(false);
    this.medicationEditorOpen.set(false);
    await this.loadMember(memberId);
  }

  protected memberPhotoUrl(m: FamilyMember): string | null {
    return m.photoPath ? `/api/family-members/${m.id}/photo` : null;
  }

  protected switchTab(tab: Tab): void {
    this.tab.set(tab);
  }

  // ─── profile ──────────────────────────────────────────────────────────────

  protected openProfileForm(): void {
    const p = this.health()?.profile;
    this.profileBloodType.set(p?.bloodType ?? '');
    this.profileAllergies.set(p?.allergies ?? '');
    this.profileConditions.set(p?.chronicConditions ?? '');
    this.profileNotes.set(p?.notes ?? '');
    this.profileEditing.set(true);
  }

  protected cancelProfileForm(): void {
    this.profileEditing.set(false);
  }

  protected onProfileBlood(event: Event): void {
    this.profileBloodType.set((event.target as HTMLInputElement).value);
  }
  protected onProfileAllergies(event: Event): void {
    this.profileAllergies.set((event.target as HTMLTextAreaElement).value);
  }
  protected onProfileConditions(event: Event): void {
    this.profileConditions.set((event.target as HTMLTextAreaElement).value);
  }
  protected onProfileNotes(event: Event): void {
    this.profileNotes.set((event.target as HTMLTextAreaElement).value);
  }

  protected async saveProfile(): Promise<void> {
    const memberId = this.selectedMemberId();
    if (!memberId || this.profileSaving()) return;
    this.profileSaving.set(true);
    this.error.set(null);
    try {
      const body: UpsertHealthProfileRequest = {
        bloodType: emptyToNull(this.profileBloodType()),
        allergies: emptyToNull(this.profileAllergies()),
        chronicConditions: emptyToNull(this.profileConditions()),
        notes: emptyToNull(this.profileNotes()),
      };
      const profile = await firstValueFrom(this.api.upsertProfile(memberId, body));
      this.health.update((h) => (h ? { ...h, profile } : h));
      this.profileEditing.set(false);
    } catch {
      this.error.set($localize`:@@health.error.save-profile:No se pudo guardar la ficha.`);
    } finally {
      this.profileSaving.set(false);
    }
  }

  // ─── vaccinations ─────────────────────────────────────────────────────────

  protected openVaccinationEditor(target: Vaccination | null): void {
    this.vaccinationEditingId.set(target?.id ?? null);
    this.vaccName.set(target?.name ?? '');
    this.vaccDate.set(target?.date ?? todayIso());
    this.vaccNextDate.set(target?.nextDueDate ?? '');
    this.vaccNotes.set(target?.notes ?? '');
    this.vaccinationEditorOpen.set(true);
  }

  protected closeVaccinationEditor(): void {
    this.vaccinationEditorOpen.set(false);
    this.vaccinationEditingId.set(null);
  }

  protected onVaccName(event: Event): void { this.vaccName.set((event.target as HTMLInputElement).value); }
  protected onVaccDate(event: Event): void { this.vaccDate.set((event.target as HTMLInputElement).value); }
  protected onVaccNextDate(event: Event): void { this.vaccNextDate.set((event.target as HTMLInputElement).value); }
  protected onVaccNotes(event: Event): void { this.vaccNotes.set((event.target as HTMLTextAreaElement).value); }

  protected async saveVaccination(): Promise<void> {
    const memberId = this.selectedMemberId();
    if (!memberId || this.vaccSaving()) return;
    if (this.vaccName().trim().length === 0 || this.vaccDate().length === 0) {
      this.error.set($localize`:@@health.error.vacc-required:Falta el nombre o la fecha de la vacuna.`);
      return;
    }
    this.vaccSaving.set(true);
    this.error.set(null);
    try {
      const body: VaccinationRequest = {
        name: this.vaccName().trim(),
        date: this.vaccDate(),
        nextDueDate: emptyToNull(this.vaccNextDate()),
        notes: emptyToNull(this.vaccNotes()),
      };
      const editingId = this.vaccinationEditingId();
      const saved = editingId
        ? await firstValueFrom(this.api.updateVaccination(editingId, body))
        : await firstValueFrom(this.api.addVaccination(memberId, body));

      this.health.update((h) => {
        if (!h) return h;
        const next = editingId
          ? h.vaccinations.map((v) => (v.id === saved.id ? saved : v))
          : [saved, ...h.vaccinations];
        next.sort((a, b) => b.date.localeCompare(a.date));
        return { ...h, vaccinations: next };
      });
      this.closeVaccinationEditor();
    } catch {
      this.error.set($localize`:@@health.error.save-vacc:No se pudo guardar la vacuna.`);
    } finally {
      this.vaccSaving.set(false);
    }
  }

  protected async deleteVaccination(v: Vaccination): Promise<void> {
    if (!window.confirm($localize`:@@health.delete-vacc-confirm:¿Borrar la vacuna "${v.name}:NAME:"?`)) return;
    try {
      await firstValueFrom(this.api.deleteVaccination(v.id));
      this.health.update((h) => h ? { ...h, vaccinations: h.vaccinations.filter((x) => x.id !== v.id) } : h);
    } catch {
      this.error.set($localize`:@@health.error.delete-vacc:No se pudo borrar la vacuna.`);
    }
  }

  // ─── medications ──────────────────────────────────────────────────────────

  protected openMedicationEditor(target: Medication | null): void {
    this.medicationEditingId.set(target?.id ?? null);
    this.medName.set(target?.name ?? '');
    this.medDose.set(target?.dose ?? '');
    this.medFreq.set(target?.frequency ?? '');
    this.medStart.set(target?.startDate ?? todayIso());
    this.medEnd.set(target?.endDate ?? '');
    this.medInstructions.set(target?.instructions ?? '');
    this.medicationEditorOpen.set(true);
  }

  protected closeMedicationEditor(): void {
    this.medicationEditorOpen.set(false);
    this.medicationEditingId.set(null);
  }

  protected onMedName(event: Event): void { this.medName.set((event.target as HTMLInputElement).value); }
  protected onMedDose(event: Event): void { this.medDose.set((event.target as HTMLInputElement).value); }
  protected onMedFreq(event: Event): void { this.medFreq.set((event.target as HTMLInputElement).value); }
  protected onMedStart(event: Event): void { this.medStart.set((event.target as HTMLInputElement).value); }
  protected onMedEnd(event: Event): void { this.medEnd.set((event.target as HTMLInputElement).value); }
  protected onMedInstructions(event: Event): void { this.medInstructions.set((event.target as HTMLTextAreaElement).value); }

  protected async saveMedication(): Promise<void> {
    const memberId = this.selectedMemberId();
    if (!memberId || this.medSaving()) return;
    if (this.medName().trim().length === 0 || this.medStart().length === 0) {
      this.error.set($localize`:@@health.error.med-required:Falta el nombre o la fecha de inicio.`);
      return;
    }
    this.medSaving.set(true);
    this.error.set(null);
    try {
      const body: MedicationRequest = {
        name: this.medName().trim(),
        dose: emptyToNull(this.medDose()),
        frequency: emptyToNull(this.medFreq()),
        startDate: this.medStart(),
        endDate: emptyToNull(this.medEnd()),
        instructions: emptyToNull(this.medInstructions()),
      };
      const editingId = this.medicationEditingId();
      const saved = editingId
        ? await firstValueFrom(this.api.updateMedication(editingId, body))
        : await firstValueFrom(this.api.addMedication(memberId, body));

      this.health.update((h) => {
        if (!h) return h;
        const next = editingId
          ? h.medications.map((m) => (m.id === saved.id ? saved : m))
          : [saved, ...h.medications];
        next.sort((a, b) => b.startDate.localeCompare(a.startDate));
        return { ...h, medications: next };
      });
      this.closeMedicationEditor();
    } catch {
      this.error.set($localize`:@@health.error.save-med:No se pudo guardar la medicación.`);
    } finally {
      this.medSaving.set(false);
    }
  }

  protected async deleteMedication(m: Medication): Promise<void> {
    if (!window.confirm($localize`:@@health.delete-med-confirm:¿Borrar la medicación "${m.name}:NAME:"?`)) return;
    try {
      await firstValueFrom(this.api.deleteMedication(m.id));
      this.health.update((h) => h ? { ...h, medications: h.medications.filter((x) => x.id !== m.id) } : h);
    } catch {
      this.error.set($localize`:@@health.error.delete-med:No se pudo borrar la medicación.`);
    }
  }

  // ─── data load ────────────────────────────────────────────────────────────

  private async loadMembers(): Promise<void> {
    this.loading.set(true);
    try {
      const list = await firstValueFrom(this.membersApi.list());
      const active = list.filter((m) => m.isActive);
      this.members.set(active);
      if (active.length > 0) {
        this.selectedMemberId.set(active[0].id);
        await this.loadMember(active[0].id);
      }
    } catch {
      this.error.set($localize`:@@health.error.load-members:No se pudieron cargar los miembros.`);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadMember(memberId: string): Promise<void> {
    this.error.set(null);
    try {
      const data = await firstValueFrom(this.api.getMember(memberId));
      this.health.set(data);
    } catch {
      this.error.set($localize`:@@health.error.load-profile:No se pudo cargar la ficha.`);
    }
  }
}

function todayIso(): string {
  const d = new Date();
  const y = d.getFullYear();
  const m = `${d.getMonth() + 1}`.padStart(2, '0');
  const day = `${d.getDate()}`.padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function emptyToNull(value: string): string | null {
  const t = value.trim();
  return t.length === 0 ? null : t;
}
