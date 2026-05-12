import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { CredentialsService } from '../../core/api/credentials.service';
import { DashboardService } from '../../core/api/dashboard.service';
import { FamilyService } from '../../core/api/family.service';
import { IntegrationApiKeysService } from '../../core/api/integration-api-keys.service';
import { NotificationPreferencesService } from '../../core/api/notification-preferences.service';
import { AuthService } from '../../core/auth/auth.service';
import { Credential } from '../../core/models/credential';
import { DashboardWidget, DashboardWidgetId } from '../../core/models/dashboard';
import { Family } from '../../core/models/family';
import { IntegrationApiKey } from '../../core/models/integration-api-key';
import { NotificationPreferences } from '../../core/models/notification-preferences';

// Widget catalogue labels live inside `widgetLabel()` so each branch goes
// through $localize and shows up in the extraction with a stable id.

/**
 * "Mi cuenta" — surfaces the user's identity (email, role) and lets them
 * manage their authentication methods. Anyone can set a local password
 * for the first time; rotating an existing one requires the current
 * password. Removing a credential is refused when it would be the last.
 */
@Component({
  selector: 'fn-account',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DatePipe, DragDropModule],
  templateUrl: './account.component.html',
  styleUrl: './account.component.css',
})
export class AccountComponent implements OnInit {
  private readonly api = inject(CredentialsService);
  private readonly prefsApi = inject(NotificationPreferencesService);
  private readonly familyApi = inject(FamilyService);
  private readonly dashboardApi = inject(DashboardService);
  private readonly integrationsApi = inject(IntegrationApiKeysService);
  protected readonly auth = inject(AuthService);

  protected readonly isAdmin = computed(() => this.auth.me()?.role === 'Admin');

  protected readonly credentials = signal<Credential[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Notification preferences — null while still loading. */
  protected readonly preferences = signal<NotificationPreferences | null>(null);
  protected readonly savingPreferences = signal(false);
  protected readonly preferencesError = signal<string | null>(null);

  /** "Send me a preview" button state + transient flash message. */
  protected readonly sendingDigest = signal(false);
  protected readonly digestFlash = signal<string | null>(null);
  private digestFlashTimer: ReturnType<typeof setTimeout> | null = null;

  /** Dashboard widget layout — null while still loading. */
  protected readonly widgets = signal<DashboardWidget[] | null>(null);
  protected readonly savingWidgets = signal(false);
  protected readonly widgetsError = signal<string | null>(null);

  /** Preferred-language picker state. Initialised from the current me() value in ngOnInit. */
  protected readonly languageDraft = signal<string>('es-ES');
  protected readonly savingLanguage = signal(false);
  protected readonly languageError = signal<string | null>(null);

  /** Currently persisted language — driven by the me() signal. */
  protected readonly currentLanguage = computed(() => this.auth.me()?.preferredLanguage ?? 'es-ES');

  /** Integration API keys — admin-only section. */
  protected readonly apiKeys = signal<IntegrationApiKey[]>([]);
  protected readonly apiKeysError = signal<string | null>(null);
  protected readonly newKeyName = signal('');
  protected readonly creatingKey = signal(false);
  /** Plaintext of the most recently minted token — kept until the user dismisses it. */
  protected readonly newKeyPlaintext = signal<string | null>(null);
  protected readonly newKeyCopyFlash = signal<string | null>(null);
  protected readonly revokingKeyId = signal<string | null>(null);

  /** Family profile — only loaded for admins (drives the location form). */
  protected readonly family = signal<Family | null>(null);
  protected readonly locationLat = signal('');
  protected readonly locationLon = signal('');
  protected readonly locationLabel = signal('');
  protected readonly savingLocation = signal(false);
  protected readonly locationError = signal<string | null>(null);
  protected readonly locationFlash = signal<string | null>(null);

  /** Form state for set-/rotate-password. */
  protected readonly formOpen = signal(false);
  protected readonly currentPassword = signal('');
  protected readonly newPassword = signal('');
  protected readonly confirmPassword = signal('');
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly successFlash = signal<string | null>(null);

  /** True when the user has a local credential (so the form must ask for currentPassword). */
  protected readonly hasLocal = computed(() =>
    this.credentials().some((c) => c.provider === 'Local'));

  /** Whether removing the credential with the given id would leave the user without methods. */
  protected canRemove(id: string): boolean {
    return this.credentials().filter((c) => c.id !== id).length > 0;
  }

  async ngOnInit(): Promise<void> {
    this.languageDraft.set(this.currentLanguage());
    const tasks: Promise<void>[] = [this.refresh(), this.loadPreferences(), this.loadWidgets()];
    if (this.isAdmin()) {
      tasks.push(this.loadFamily());
      tasks.push(this.loadApiKeys());
    }
    await Promise.all(tasks);
  }

  // ─── Preferred-language picker ─────────────────────────────────────────────

  protected onLanguageDraftChange(event: Event): void {
    this.languageDraft.set((event.target as HTMLSelectElement).value);
  }

  protected async saveLanguage(): Promise<void> {
    if (this.savingLanguage()) return;
    if (this.languageDraft() === this.currentLanguage()) return;

    this.savingLanguage.set(true);
    this.languageError.set(null);
    try {
      // Reloads the page on success — no further work needed here.
      await this.auth.setPreferredLanguage(this.languageDraft());
    } catch {
      this.languageError.set($localize`:@@account.language.error:No se pudo cambiar el idioma. Inténtalo de nuevo.`);
      this.savingLanguage.set(false);
    }
  }

  // ─── Integration API keys ──────────────────────────────────────────────────

  private async loadApiKeys(): Promise<void> {
    try {
      const list = await firstValueFrom(this.integrationsApi.list());
      this.apiKeys.set(list);
    } catch {
      this.apiKeysError.set($localize`:@@account.api-keys.error.load:No se pudieron cargar las claves de integración.`);
    }
  }

  protected onNewKeyNameInput(event: Event): void {
    this.newKeyName.set((event.target as HTMLInputElement).value);
  }

  protected async createApiKey(): Promise<void> {
    if (this.creatingKey()) return;
    const name = this.newKeyName().trim();
    if (name.length === 0) {
      this.apiKeysError.set($localize`:@@account.api-keys.error.no-name:Dale un nombre a la clave (p. ej. "atajo de iOS").`);
      return;
    }

    this.creatingKey.set(true);
    this.apiKeysError.set(null);
    try {
      const created = await firstValueFrom(this.integrationsApi.create(name));
      // Keep the plaintext on screen until the user dismisses it. The list
      // refresh happens after so the new row already shows the prefix.
      this.newKeyPlaintext.set(created.token);
      this.newKeyName.set('');
      await this.loadApiKeys();
    } catch {
      this.apiKeysError.set($localize`:@@account.api-keys.error.create:No se pudo crear la clave.`);
    } finally {
      this.creatingKey.set(false);
    }
  }

  protected async copyPlaintext(): Promise<void> {
    const token = this.newKeyPlaintext();
    if (!token) return;
    try {
      await navigator.clipboard.writeText(token);
      this.newKeyCopyFlash.set($localize`:@@account.api-keys.copied:Copiado.`);
    } catch {
      this.newKeyCopyFlash.set($localize`:@@account.api-keys.copy-failed:No se pudo copiar — selecciona el texto manualmente.`);
    }
    setTimeout(() => this.newKeyCopyFlash.set(null), 3000);
  }

  protected dismissPlaintext(): void {
    this.newKeyPlaintext.set(null);
    this.newKeyCopyFlash.set(null);
  }

  protected async revokeApiKey(key: IntegrationApiKey): Promise<void> {
    if (this.revokingKeyId()) return;
    const msg = $localize`:@@account.api-keys.revoke-confirm:¿Revocar la clave "${key.name}:NAME:"? Cualquier integración que la use dejará de funcionar.`;
    if (!window.confirm(msg)) {
      return;
    }
    this.revokingKeyId.set(key.id);
    this.apiKeysError.set(null);
    try {
      await firstValueFrom(this.integrationsApi.revoke(key.id));
      await this.loadApiKeys();
    } catch {
      this.apiKeysError.set($localize`:@@account.api-keys.error.revoke:No se pudo revocar la clave.`);
    } finally {
      this.revokingKeyId.set(null);
    }
  }

  // ─── Dashboard widgets ─────────────────────────────────────────────────────

  protected widgetLabel(id: DashboardWidgetId): string {
    switch (id) {
      case 'weather': return $localize`:@@account.widgets.weather:Tiempo`;
      case 'school': return $localize`:@@account.widgets.school:Hoy en el cole`;
      case 'agenda': return $localize`:@@account.widgets.agenda:Hoy fuera de casa`;
      case 'tasks': return $localize`:@@account.widgets.tasks:Tareas de hoy`;
      case 'calendar': return $localize`:@@account.widgets.calendar:Próximos eventos`;
      case 'meals': return $localize`:@@account.widgets.meals:A la mesa`;
      case 'wall': return $localize`:@@account.widgets.wall:Fijados en el muro`;
      case 'scores': return $localize`:@@account.widgets.scores:Marcador de la semana`;
      case 'birthdays': return $localize`:@@account.widgets.birthdays:Cumpleaños próximos`;
    }
  }

  /** Tooltip surfaced on the disabled "Eliminar" button when it's the last credential. */
  protected readonly cannotRemoveTooltip = $localize`:@@account.credentials.cannot-remove:No puedes eliminar el último método.`;

  private async loadWidgets(): Promise<void> {
    try {
      const prefs = await firstValueFrom(this.dashboardApi.getPreferences());
      this.widgets.set(prefs.widgets);
    } catch {
      this.widgetsError.set($localize`:@@account.widgets.error.load:No se pudieron cargar los widgets del panel.`);
    }
  }

  /** Persist the current widget order (no optimistic UI — the operation is fast and rare). */
  private async saveWidgets(): Promise<void> {
    const list = this.widgets();
    if (!list || this.savingWidgets()) return;

    this.savingWidgets.set(true);
    this.widgetsError.set(null);
    try {
      const saved = await firstValueFrom(this.dashboardApi.updatePreferences({ widgets: list }));
      this.widgets.set(saved.widgets);
    } catch {
      this.widgetsError.set($localize`:@@account.widgets.error.save:No se pudieron guardar los widgets.`);
    } finally {
      this.savingWidgets.set(false);
    }
  }

  /** Toggle visibility of a widget by id. Persists the new layout. */
  protected async toggleWidget(id: DashboardWidgetId): Promise<void> {
    const list = this.widgets();
    if (!list) return;
    this.widgets.set(list.map((w) => w.id === id ? { ...w, visible: !w.visible } : w));
    await this.saveWidgets();
  }

  /** Apply a CDK drag-drop move and persist the new order. */
  protected async onWidgetDrop(event: CdkDragDrop<DashboardWidget[]>): Promise<void> {
    const list = this.widgets();
    if (!list || event.previousIndex === event.currentIndex) return;
    const next = list.slice();
    moveItemInArray(next, event.previousIndex, event.currentIndex);
    this.widgets.set(next);
    await this.saveWidgets();
  }

  private async loadFamily(): Promise<void> {
    try {
      const f = await firstValueFrom(this.familyApi.get());
      this.family.set(f);
      this.locationLat.set(f.latitude !== null ? String(f.latitude) : '');
      this.locationLon.set(f.longitude !== null ? String(f.longitude) : '');
      this.locationLabel.set(f.locationLabel ?? '');
    } catch {
      this.locationError.set($localize`:@@account.location.error.load:No se pudieron cargar los datos de la familia.`);
    }
  }

  protected onLatInput(event: Event): void {
    this.locationLat.set((event.target as HTMLInputElement).value);
  }

  protected onLonInput(event: Event): void {
    this.locationLon.set((event.target as HTMLInputElement).value);
  }

  protected onLocationLabelInput(event: Event): void {
    this.locationLabel.set((event.target as HTMLInputElement).value);
  }

  /** Save the entered lat/lon/label. Empty pair clears the location. */
  protected async saveLocation(): Promise<void> {
    if (this.savingLocation()) return;

    const latRaw = this.locationLat().trim();
    const lonRaw = this.locationLon().trim();

    if ((latRaw === '') !== (lonRaw === '')) {
      this.locationError.set($localize`:@@account.location.error.both-required:Latitud y longitud deben rellenarse o vaciarse a la vez.`);
      return;
    }

    let lat: number | null = null;
    let lon: number | null = null;
    if (latRaw !== '') {
      lat = Number(latRaw.replace(',', '.'));
      lon = Number(lonRaw.replace(',', '.'));
      if (!Number.isFinite(lat) || !Number.isFinite(lon)) {
        this.locationError.set($localize`:@@account.location.error.not-numbers:Latitud y longitud deben ser números.`);
        return;
      }
      if (lat < -90 || lat > 90 || lon < -180 || lon > 180) {
        this.locationError.set($localize`:@@account.location.error.out-of-range:Coordenadas fuera de rango (lat ±90, lon ±180).`);
        return;
      }
    }

    const label = this.locationLabel().trim();

    this.savingLocation.set(true);
    this.locationError.set(null);
    this.locationFlash.set(null);
    try {
      const updated = await firstValueFrom(this.familyApi.updateLocation({
        latitude: lat,
        longitude: lon,
        locationLabel: label === '' ? null : label,
      }));
      this.family.set(updated);
      this.locationFlash.set(lat === null
        ? $localize`:@@account.location.flash.cleared:Ubicación borrada.`
        : $localize`:@@account.location.flash.saved:Ubicación guardada.`);
    } catch {
      this.locationError.set($localize`:@@account.location.error.save:No se pudo guardar la ubicación.`);
    } finally {
      this.savingLocation.set(false);
    }
  }

  private async loadPreferences(): Promise<void> {
    try {
      const prefs = await firstValueFrom(this.prefsApi.get());
      this.preferences.set(prefs);
    } catch {
      this.preferencesError.set($localize`:@@account.notifications.error.load:No se pudieron cargar las preferencias.`);
    }
  }

  /**
   * Toggle a single field and persist the whole row. The optimistic update
   * lets the checkbox feel instant; on failure we restore the previous state.
   */
  protected async togglePreference(field: keyof NotificationPreferences): Promise<void> {
    const current = this.preferences();
    if (!current || this.savingPreferences()) return;

    const next: NotificationPreferences = { ...current, [field]: !current[field] };
    this.preferences.set(next);
    this.savingPreferences.set(true);
    this.preferencesError.set(null);

    try {
      const saved = await firstValueFrom(this.prefsApi.update(next));
      this.preferences.set(saved);
    } catch {
      // Revert the optimistic flip and surface the error.
      this.preferences.set(current);
      this.preferencesError.set($localize`:@@account.notifications.error.save:No se pudo guardar la preferencia.`);
    } finally {
      this.savingPreferences.set(false);
    }
  }

  /**
   * Trigger an out-of-band digest email to the calling user only — does not
   * mark today as "already sent" so the morning scheduler still runs.
   * Useful for previewing the template after design changes.
   */
  protected async sendDigestPreview(): Promise<void> {
    if (this.sendingDigest()) return;
    this.sendingDigest.set(true);
    if (this.digestFlashTimer) clearTimeout(this.digestFlashTimer);
    this.digestFlash.set(null);
    try {
      const result = await firstValueFrom(this.prefsApi.sendMyDigest());
      this.digestFlash.set(result.isEmpty
        ? $localize`:@@account.digest-preview.flash.empty:Hoy el digest no tiene contenido (ningún email enviado).`
        : $localize`:@@account.digest-preview.flash.sent:Enviado a ${result.email}:EMAIL:.`);
    } catch {
      this.digestFlash.set($localize`:@@account.digest-preview.flash.error:No se pudo enviar — revisa la configuración SMTP.`);
    } finally {
      this.sendingDigest.set(false);
      this.digestFlashTimer = setTimeout(() => this.digestFlash.set(null), 6000);
    }
  }

  protected async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const list = await firstValueFrom(this.api.list());
      this.credentials.set(list);
    } catch {
      this.error.set($localize`:@@account.error.load-credentials:No se pudieron cargar tus métodos de inicio de sesión.`);
    } finally {
      this.loading.set(false);
    }
  }

  protected openForm(): void {
    this.currentPassword.set('');
    this.newPassword.set('');
    this.confirmPassword.set('');
    this.formError.set(null);
    this.formOpen.set(true);
  }

  protected closeForm(): void {
    this.formOpen.set(false);
  }

  protected async submitForm(): Promise<void> {
    if (this.submitting()) return;

    const np = this.newPassword();
    if (np.length < 8) {
      this.formError.set($localize`:@@account.password-form.error.too-short:La contraseña debe tener al menos 8 caracteres.`);
      return;
    }
    if (!/[a-zA-Z]/.test(np) || !/\d/.test(np)) {
      this.formError.set($localize`:@@account.password-form.error.weak:La contraseña debe contener al menos una letra y un número.`);
      return;
    }
    if (np !== this.confirmPassword()) {
      this.formError.set($localize`:@@account.password-form.error.mismatch:Las dos contraseñas no coinciden.`);
      return;
    }

    if (this.hasLocal() && this.currentPassword().length === 0) {
      this.formError.set($localize`:@@account.password-form.error.current-required:Introduce tu contraseña actual para cambiarla.`);
      return;
    }

    this.submitting.set(true);
    this.formError.set(null);
    try {
      await firstValueFrom(this.api.setPassword({
        currentPassword: this.hasLocal() ? this.currentPassword() : null,
        newPassword: np,
      }));
      this.formOpen.set(false);
      this.successFlash.set(this.hasLocal()
        ? $localize`:@@account.password-form.flash.updated:Contraseña actualizada.`
        : $localize`:@@account.password-form.flash.created:Contraseña local creada. Ya puedes iniciar sesión con email y contraseña.`);
      await this.refresh();
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      if (status === 403) {
        this.formError.set($localize`:@@account.password-form.error.wrong-current:La contraseña actual no es correcta.`);
      } else if (status === 400) {
        this.formError.set($localize`:@@account.password-form.error.invalid:La contraseña no cumple los requisitos.`);
      } else {
        this.formError.set($localize`:@@account.password-form.error.save-unknown:No se pudo guardar la contraseña. Inténtalo de nuevo.`);
      }
    } finally {
      this.submitting.set(false);
    }
  }

  protected async remove(credential: Credential): Promise<void> {
    if (!this.canRemove(credential.id)) {
      return;
    }
    const label = this.providerLabel(credential.provider);
    const removeMsg = $localize`:@@account.credentials.remove-confirm:¿Eliminar el método "${label}:LABEL:"? Podrás añadirlo de nuevo más tarde.`;
    if (!window.confirm(removeMsg)) {
      return;
    }
    try {
      await firstValueFrom(this.api.remove(credential.id));
      this.successFlash.set($localize`:@@account.credentials.flash.removed:Método de inicio de sesión eliminado.`);
      await this.refresh();
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      this.error.set(status === 409
        ? $localize`:@@account.credentials.error.last:No puedes eliminar el último método de inicio de sesión.`
        : $localize`:@@account.credentials.error.remove:No se pudo eliminar el método.`);
    }
  }

  protected providerLabel(provider: Credential['provider']): string {
    return provider === 'Oidc' ? 'PocketID' : $localize`:@@account.credentials.provider.local:Contraseña local`;
  }

  protected logout(): void {
    void this.auth.logout();
  }

  protected readCurrent(event: Event): void {
    this.currentPassword.set((event.target as HTMLInputElement).value);
  }

  protected readNew(event: Event): void {
    this.newPassword.set((event.target as HTMLInputElement).value);
  }

  protected readConfirm(event: Event): void {
    this.confirmPassword.set((event.target as HTMLInputElement).value);
  }
}
