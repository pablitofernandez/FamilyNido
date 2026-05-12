import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { InvitationsService } from '../../core/api/invitations.service';
import { AuthService } from '../../core/auth/auth.service';
import { InvitationPreview } from '../../core/models/invitation';

/**
 * Public landing page for an invitation link. Behavior depends on the
 * caller's auth status:
 *  - anonymous: shows a preview ("Dan te invita a la familia X") and two
 *    options — log in with PocketID, or set a brand-new local password.
 *  - not-linked: the caller is logged in via OIDC but still without a
 *    member; auto-redeem the invitation.
 *  - authenticated: the caller is already in a family, so accepting another
 *    invitation is refused.
 */
@Component({
  selector: 'fn-accept-invitation',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink],
  templateUrl: './accept-invitation.component.html',
  styleUrl: './accept-invitation.component.css',
})
export class AcceptInvitationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(InvitationsService);
  protected readonly auth = inject(AuthService);

  protected readonly loading = signal(true);
  protected readonly preview = signal<InvitationPreview | null>(null);
  protected readonly fatal = signal<string | null>(null);

  protected readonly password = signal('');
  protected readonly passwordConfirm = signal('');
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  /** Convenience for the template — true when the auth bootstrap finished. */
  protected readonly authReady = computed(() => this.auth.status() !== 'idle' && this.auth.status() !== 'loading');

  /** True when the invitation can still be redeemed. */
  protected readonly isPending = computed(() => this.preview()?.status === 'Pending');

  private token = '';

  async ngOnInit(): Promise<void> {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    if (!this.token) {
      this.fatal.set('not_found');
      this.loading.set(false);
      return;
    }

    // Make sure the auth signal is hydrated before we decide what to do.
    if (!this.authReady()) {
      await this.auth.loadInitialSession();
    }

    try {
      const preview = await firstValueFrom(this.api.preview(this.token));
      this.preview.set(preview);
    } catch {
      this.fatal.set('not_found');
      this.loading.set(false);
      return;
    }

    // Auto-accept when the user came back from the OIDC callback already
    // logged in but still without a family member.
    if (this.auth.status() === 'not-linked' && this.preview()?.status === 'Pending') {
      await this.acceptViaOidc();
    }

    this.loading.set(false);
  }

  /** Step 1 of the OIDC path: bounce through the login redirect. */
  protected loginWithOidc(): void {
    this.auth.loginOidc(`/invite/${this.token}`);
  }

  /** Step 2 of the OIDC path: redeem the token using the freshly minted cookie. */
  private async acceptViaOidc(): Promise<void> {
    try {
      await firstValueFrom(this.api.acceptOidc(this.token));
      await this.auth.loadInitialSession();
      void this.router.navigateByUrl('/home');
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      this.fatal.set(this.mapAcceptError(status));
    }
  }

  /** Local-password path: create user + cookie in one POST. */
  protected async acceptViaLocal(): Promise<void> {
    if (this.submitting()) return;

    const pw = this.password();
    const confirm = this.passwordConfirm();

    if (pw.length < 8) {
      this.formError.set($localize`:@@invitation.error.password-too-short:La contraseña debe tener al menos 8 caracteres.`);
      return;
    }
    if (!/[a-zA-Z]/.test(pw) || !/\d/.test(pw)) {
      this.formError.set($localize`:@@invitation.error.password-weak:La contraseña debe contener al menos una letra y un número.`);
      return;
    }
    if (pw !== confirm) {
      this.formError.set($localize`:@@invitation.error.password-mismatch:Las dos contraseñas no coinciden.`);
      return;
    }

    this.submitting.set(true);
    this.formError.set(null);
    try {
      await firstValueFrom(this.api.acceptLocal(this.token, pw));
      await this.auth.loadInitialSession();
      void this.router.navigateByUrl('/home');
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      if (status === 409) {
        this.formError.set($localize`:@@invitation.error.unavailable-detailed:Esta invitación ya no está disponible (caducada, usada o revocada).`);
      } else if (status === 400) {
        this.formError.set($localize`:@@invitation.error.password-invalid:La contraseña no cumple los requisitos.`);
      } else {
        this.formError.set($localize`:@@invitation.error.submit-unknown:No se pudo completar el alta. Inténtalo de nuevo.`);
      }
    } finally {
      this.submitting.set(false);
    }
  }

  protected readPassword(event: Event): void {
    this.password.set((event.target as HTMLInputElement).value);
  }

  protected readPasswordConfirm(event: Event): void {
    this.passwordConfirm.set((event.target as HTMLInputElement).value);
  }

  protected logout(): void {
    void this.auth.logout();
  }

  protected statusMessage(): string {
    switch (this.preview()?.status) {
      case 'Consumed':
        return $localize`:@@invitation.status.consumed:Esta invitación ya se ha utilizado.`;
      case 'Revoked':
        return $localize`:@@invitation.status.revoked:Esta invitación ha sido revocada por el administrador.`;
      case 'Expired':
        return $localize`:@@invitation.status.expired:Esta invitación ha caducado. Pide al administrador que te envíe una nueva.`;
      default:
        return '';
    }
  }

  private mapAcceptError(status: number | undefined): string {
    if (status === 409) return 'unavailable';
    return 'unknown';
  }

  protected fatalMessage(): string {
    switch (this.fatal()) {
      case 'not_found':
        return $localize`:@@invitation.fatal.not-found:Esta invitación no existe o el enlace es incorrecto.`;
      case 'unavailable':
        return $localize`:@@invitation.fatal.unavailable:Esta invitación ya no está disponible.`;
      default:
        return $localize`:@@invitation.fatal.unknown:Algo salió mal. Inténtalo de nuevo más tarde.`;
    }
  }
}
