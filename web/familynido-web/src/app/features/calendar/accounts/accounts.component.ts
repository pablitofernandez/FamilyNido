import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { CalendarService } from '../../../core/api/calendar.service';
import { FamilyMembersService } from '../../../core/api/family-members.service';
import { GoogleAccount, LinkedCalendar } from '../../../core/models/calendar';
import { FamilyMember } from '../../../core/models/family-member';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

/**
 * Cuentas vinculadas — settings screen for the calendar module.
 *
 * Lists every linked Google account, lets adults pick which calendars to mirror,
 * assign each calendar to a family member (for color attribution in the agenda),
 * trigger a manual sync, or unlink the whole account. The "+" button kicks off
 * the OAuth dance via /api/calendar/google/start.
 *
 * The component also shows the contextual outcome of a callback redirect: when
 * the user comes back from Google, the URL carries either ?linked=<id> or
 * ?error=<code>; both are surfaced as a toast-like banner.
 */
@Component({
  selector: 'fn-calendar-accounts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, IconComponent],
  templateUrl: './accounts.component.html',
  styleUrl: './accounts.component.css',
})
export class AccountsComponent implements OnInit {
  private readonly api = inject(CalendarService);
  private readonly membersApi = inject(FamilyMembersService);
  private readonly route = inject(ActivatedRoute);

  /** Aria-label for the per-calendar member-assign dropdown. */
  protected memberAssignAriaLabel(calendarSummary: string): string {
    return $localize`:@@calendar.accounts.member-assign-aria:Miembro asignado a ${calendarSummary}:NAME:`;
  }
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly accounts = signal<GoogleAccount[]>([]);
  protected readonly members = signal<FamilyMember[]>([]);
  protected readonly busyAccountId = signal<string | null>(null);
  protected readonly banner = signal<{ kind: 'success' | 'error'; message: string } | null>(null);

  protected readonly hasAccounts = computed(() => this.accounts().length > 0);

  ngOnInit(): void {
    this.applyRedirectBanner();
    this.load();
  }

  protected async startLink(): Promise<void> {
    try {
      const response = await firstValueFrom(this.api.startGoogleLink());
      // Full-page navigation so the OAuth state cookie is included in the
      // top-level request flow back from Google.
      window.location.href = response.authUrl;
    } catch {
      this.banner.set({ kind: 'error', message: 'No se pudo iniciar la vinculación con Google.' });
    }
  }

  protected toggleImport(account: GoogleAccount, calendar: LinkedCalendar): void {
    this.busyAccountId.set(account.id);
    this.api
      .updateCalendar(calendar.id, {
        isImported: !calendar.isImported,
        familyMemberId: calendar.familyMemberId,
      })
      .subscribe({
        next: (updated) => this.patchCalendar(account.id, updated),
        error: () => this.banner.set({ kind: 'error', message: 'No se pudo actualizar el calendario.' }),
        complete: () => this.busyAccountId.set(null),
      });
  }

  protected assignMember(account: GoogleAccount, calendar: LinkedCalendar, memberId: string): void {
    const normalized = memberId === '' ? null : memberId;
    this.busyAccountId.set(account.id);
    this.api
      .updateCalendar(calendar.id, {
        isImported: calendar.isImported,
        familyMemberId: normalized,
      })
      .subscribe({
        next: (updated) => this.patchCalendar(account.id, updated),
        error: () => this.banner.set({ kind: 'error', message: 'No se pudo asignar el miembro.' }),
        complete: () => this.busyAccountId.set(null),
      });
  }

  protected sync(account: GoogleAccount): void {
    this.busyAccountId.set(account.id);
    this.api.triggerManualSync(account.id).subscribe({
      next: (refreshed) => {
        this.accounts.update((list) =>
          list.map((a) => (a.id === refreshed.id ? refreshed : a)),
        );
        this.banner.set({ kind: 'success', message: `Sincronización completada para ${refreshed.email}.` });
      },
      error: () => this.banner.set({ kind: 'error', message: 'La sincronización ha fallado.' }),
      complete: () => this.busyAccountId.set(null),
    });
  }

  protected unlink(account: GoogleAccount): void {
    if (!window.confirm(`¿Desvincular ${account.email}? Se eliminarán los eventos sincronizados.`)) {
      return;
    }

    this.busyAccountId.set(account.id);
    this.api.unlinkAccount(account.id).subscribe({
      next: () => {
        this.accounts.update((list) => list.filter((a) => a.id !== account.id));
        this.banner.set({ kind: 'success', message: 'Cuenta desvinculada.' });
      },
      error: () => this.banner.set({ kind: 'error', message: 'No se pudo desvincular la cuenta.' }),
      complete: () => this.busyAccountId.set(null),
    });
  }

  private readonly locale = inject(LOCALE_ID);

  protected memberName(memberId: string | null): string {
    if (!memberId) return $localize`:@@calendar.accounts.unassigned-fallback:Sin asignar`;
    return this.members().find((m) => m.id === memberId)?.displayName ?? '—';
  }

  protected lastSyncLabel(calendar: LinkedCalendar): string {
    if (!calendar.lastSyncedAt) return $localize`:@@calendar.accounts.never-synced:Sin sincronizar`;
    const d = new Date(calendar.lastSyncedAt);
    const when = d.toLocaleString(this.locale, {
      day: 'numeric',
      month: 'short',
      // `numeric` lets the locale pick 12H vs 24H itself (issue #12).
      hour: 'numeric',
      minute: '2-digit',
    });
    return $localize`:@@calendar.accounts.last-sync:Última sync · ${when}:WHEN:`;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [accounts, members] = await Promise.all([
        firstValueFrom(this.api.listAccounts()),
        firstValueFrom(this.membersApi.list()),
      ]);
      this.accounts.set(accounts);
      this.members.set(members);
    } catch {
      this.error.set('No se pudieron cargar las cuentas vinculadas.');
    } finally {
      this.loading.set(false);
    }
  }

  private patchCalendar(accountId: string, updated: LinkedCalendar): void {
    this.accounts.update((list) =>
      list.map((account) =>
        account.id !== accountId
          ? account
          : {
              ...account,
              calendars: account.calendars.map((c) => (c.id === updated.id ? updated : c)),
            },
      ),
    );
  }

  private applyRedirectBanner(): void {
    const params = this.route.snapshot.queryParamMap;
    const linked = params.get('linked');
    const error = params.get('error');

    if (linked) {
      this.banner.set({
        kind: 'success',
        message: 'Cuenta de Google vinculada. Selecciona qué calendarios importar.',
      });
    } else if (error) {
      this.banner.set({ kind: 'error', message: this.errorLabel(error) });
    }

    if (linked || error) {
      // Drop the query params so a refresh doesn't replay the banner.
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {},
        replaceUrl: true,
      });
    }
  }

  private errorLabel(code: string): string {
    switch (code) {
      case 'calendar.oauth_state_invalid':
        return 'La sesión de vinculación había expirado. Inténtalo de nuevo.';
      case 'calendar.token_exchange_failed':
        return 'Google rechazó el código de autorización. Inténtalo de nuevo.';
      case 'calendar.missing_refresh_token':
        return 'Google no devolvió un refresh token. Revoca el acceso en https://myaccount.google.com/permissions y reintenta.';
      case 'calendar.list_failed':
        return 'No se pudo recuperar la lista de calendarios. Revisa los permisos otorgados.';
      case 'calendar.user_not_linked':
        return 'Tu usuario no está vinculado a un miembro familiar. Pide al admin que te vincule.';
      case 'access_denied':
        return 'Has cancelado la autorización en Google.';
      default:
        return `Error en la vinculación (${code}).`;
    }
  }
}
