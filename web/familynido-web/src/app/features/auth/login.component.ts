import { ChangeDetectionStrategy, Component, LOCALE_ID, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

/**
 * Login chooser shown to anonymous users. Offers two routes:
 *  - "Continuar con PocketID" → full-page OIDC challenge.
 *  - "Iniciar sesión con email" → local credentials POST that returns a
 *    cookie of the same shape as the OIDC callback would produce.
 *
 * Sits outside the AppShell, has no auth guard, and forwards the
 * `returnUrl` query param so the user lands back where they were going.
 */
@Component({
  selector: 'fn-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly locale = inject(LOCALE_ID);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly submitting = signal(false);
  protected readonly error = signal<'invalid_credentials' | 'rate_limited' | 'unknown' | null>(null);
  // Optimistic default: assume OIDC is on until the server says otherwise,
  // so a slow /api/auth/providers response doesn't flash a "local only" UI
  // on a deployment that does have PocketID configured.
  protected readonly oidcEnabled = signal(true);

  private returnUrl = '/';

  ngOnInit(): void {
    const candidate = this.route.snapshot.queryParamMap.get('returnUrl');
    // Only accept same-origin paths (start with "/") so a crafted returnUrl
    // can't redirect off-site after a successful login.
    if (candidate && candidate.startsWith('/') && !candidate.startsWith('//')) {
      this.returnUrl = candidate;
    }
    // The auth guard prepends the locale prefix on its way in — but a
    // bookmark or stale URL may carry an unprefixed `returnUrl` (e.g.
    // `/home`). Fix it up here so both login paths land somewhere nginx
    // recognises.
    if (!/^\/(es|en)(\/|$)/.test(this.returnUrl)) {
      const prefix = this.locale.toLowerCase().startsWith('en') ? '/en' : '/es';
      this.returnUrl = this.returnUrl === '/' ? prefix : prefix + this.returnUrl;
    }

    // Ask the server which login providers it actually has wired up. Hides
    // the OIDC button when no OIDC authority is configured.
    void this.auth.getProviders().then(({ oidcEnabled }) => this.oidcEnabled.set(oidcEnabled));
  }

  protected loginOidc(): void {
    this.auth.loginOidc(this.returnUrl);
  }

  protected async loginLocal(): Promise<void> {
    if (this.submitting()) return;

    const email = this.email().trim();
    const password = this.password();
    if (email.length === 0 || password.length === 0) {
      this.error.set('invalid_credentials');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const outcome = await this.auth.loginLocal(email, password);

    this.submitting.set(false);

    if (outcome.ok) {
      // Full-page navigation keeps the returnUrl honest about the locale
      // prefix — `router.navigateByUrl` would interpret `/es/home` as a
      // router path and fail to match it (the base href has already
      // consumed `/es/`). A reload also re-bootstraps the SPA against the
      // freshly minted cookie, which is cheap on a one-shot login event.
      window.location.assign(this.returnUrl);
      return;
    }
    this.error.set(outcome.reason);
  }

  protected readEmail(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
  }

  protected readPassword(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
  }

  protected errorMessage(): string {
    switch (this.error()) {
      case 'invalid_credentials':
        return $localize`:@@auth.login.error.invalid-credentials:Email o contraseña incorrectos.`;
      case 'rate_limited':
        return $localize`:@@auth.login.error.rate-limited:Demasiados intentos. Espera unos minutos antes de volver a intentarlo.`;
      case 'unknown':
        return $localize`:@@auth.login.error.unknown:No se pudo iniciar sesión. Inténtalo de nuevo.`;
      default:
        return '';
    }
  }
}
