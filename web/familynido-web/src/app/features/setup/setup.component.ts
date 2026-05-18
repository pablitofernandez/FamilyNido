import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AuthService } from '../../core/auth/auth.service';

/**
 * First-run setup wizard (issue #20). Shown to anonymous visitors when
 * `/api/setup/status` reports the instance has zero users — i.e. a
 * freshly-deployed self-hosted installation that hasn't been bootstrapped.
 *
 * Collects the minimum needed to create the family + admin user + linked
 * member + local password credential, hits `/api/setup/initial-admin`,
 * then auto-logs the admin in so they land straight on the dashboard.
 * Locale defaults to `en-US` and timezone defaults to the browser's
 * IANA zone — both can be tweaked later from `/account`.
 */
@Component({
  selector: 'fn-setup',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule],
  templateUrl: './setup.component.html',
  styleUrl: './setup.component.css',
})
export class SetupComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly locale = inject(LOCALE_ID);

  protected readonly familyName = signal('');
  protected readonly timeZone = signal('');
  protected readonly adminEmail = signal('');
  protected readonly adminDisplayName = signal('');
  protected readonly password = signal('');
  protected readonly passwordConfirm = signal('');

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    // Best-effort browser-timezone default; the user can edit it before
    // submitting. Empty if the API isn't available (very old browsers).
    try {
      this.timeZone.set(Intl.DateTimeFormat().resolvedOptions().timeZone ?? '');
    } catch {
      this.timeZone.set('');
    }
  }

  protected async submit(): Promise<void> {
    if (this.submitting()) return;

    const pwd = this.password();
    if (pwd !== this.passwordConfirm()) {
      this.error.set($localize`:@@setup.error.password-mismatch:Las contraseñas no coinciden.`);
      return;
    }
    if (pwd.length < 8 || !/\d/.test(pwd) || !/[a-zA-Z]/.test(pwd)) {
      this.error.set($localize`:@@setup.error.password-weak:La contraseña debe tener al menos 8 caracteres con letras y un número.`);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.auth.initializeAdmin({
        family: {
          name: this.familyName().trim(),
          timeZone: this.timeZone().trim(),
        },
        admin: {
          email: this.adminEmail().trim(),
          displayName: this.adminDisplayName().trim(),
          password: pwd,
        },
      });
      // Full-page navigation so the SPA re-bootstraps with the freshly
      // minted cookie and the locale prefix matches the active bundle.
      const prefix = this.locale.toLowerCase().startsWith('en') ? '/en' : '/es';
      window.location.assign(prefix + '/home');
    } catch (err: unknown) {
      const status = (err as { status?: number }).status;
      this.error.set(status === 409
        ? $localize`:@@setup.error.already-initialised:Esta instancia ya está inicializada. Recarga la página para iniciar sesión.`
        : $localize`:@@setup.error.unknown:No se pudo completar la configuración. Revisa los datos e inténtalo de nuevo.`);
      this.submitting.set(false);
    }
  }

  protected read(setter: (v: string) => void) {
    return (event: Event) => setter((event.target as HTMLInputElement).value);
  }

  protected get familyNameSetter() { return (v: string) => this.familyName.set(v); }
  protected get timeZoneSetter() { return (v: string) => this.timeZone.set(v); }
  protected get adminEmailSetter() { return (v: string) => this.adminEmail.set(v); }
  protected get adminDisplayNameSetter() { return (v: string) => this.adminDisplayName.set(v); }
  protected get passwordSetter() { return (v: string) => this.password.set(v); }
  protected get passwordConfirmSetter() { return (v: string) => this.passwordConfirm.set(v); }
}
