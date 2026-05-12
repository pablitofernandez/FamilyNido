import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { FamilyMembersService } from '../../core/api/family-members.service';
import { InvitationsService } from '../../core/api/invitations.service';
import { FamilyMember, FamilyRole, MemberType } from '../../core/models/family-member';
import { CreateInvitationResponse, Invitation } from '../../core/models/invitation';
import { AuthService } from '../../core/auth/auth.service';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { memberSubtitle } from './member-formatting';

/** Inline form state for adding a new member (with optional invitation). */
interface AddForm {
  displayName: string;
  memberType: MemberType;
  colorHex: string;
  birthDate: string;
  contactEmail: string;
  sendInvitation: boolean;
  inviteAsAdmin: boolean;
}

/**
 * "El Nido" — family roster screen. Admin-only actions: add a member
 * (optionally inviting them by email in the same step), see and manage
 * pending invitations, send a fresh invitation to an unlinked adult member.
 * Read-only for non-admins (RF-USR-009).
 */
@Component({
  selector: 'fn-nido',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent, IconComponent, FormsModule, DatePipe, RouterLink],
  templateUrl: './nido.component.html',
  styleUrl: './nido.component.css',
})
export class NidoComponent implements OnInit {
  private readonly api = inject(FamilyMembersService);
  private readonly invitationsApi = inject(InvitationsService);
  private readonly auth = inject(AuthService);

  protected readonly members = signal<FamilyMember[]>([]);
  protected readonly invitations = signal<Invitation[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly addOpen = signal(false);
  protected readonly addForm = signal<AddForm>(this.emptyForm());
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  /** Set when an invitation was just created — surfaces the copy-link banner. */
  protected readonly lastInvitation = signal<CreateInvitationResponse | null>(null);

  protected readonly isAdmin = computed(() => this.auth.me()?.role === 'Admin');

  protected readonly subtitle = computed(() => {
    const n = this.members().length;
    if (n === 0) return $localize`:@@nido.subtitle.empty:Sin miembros todavía`;
    return n === 1
      ? $localize`:@@nido.subtitle.one:${n}:N: miembro`
      : $localize`:@@nido.subtitle.many:${n}:N: miembros`;
  });

  ngOnInit(): void {
    void this.refreshAll();
  }

  // ─── data loading ────────────────────────────────────────────────────────

  protected async refreshAll(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [members, invitations] = await Promise.all([
        firstValueFrom(this.api.list({ includeInactive: true })),
        this.isAdmin() ? firstValueFrom(this.invitationsApi.list()) : Promise.resolve([] as Invitation[]),
      ]);
      this.members.set(members);
      this.invitations.set(invitations);
    } catch {
      this.error.set('error');
    } finally {
      this.loading.set(false);
    }
  }

  // ─── add-member form ────────────────────────────────────────────────────

  protected openAdd(): void {
    this.addForm.set(this.emptyForm());
    this.formError.set(null);
    this.addOpen.set(true);
  }

  protected closeAdd(): void {
    this.addOpen.set(false);
  }

  protected updateForm<K extends keyof AddForm>(field: K, value: AddForm[K]): void {
    this.addForm.update((f) => ({ ...f, [field]: value }));
  }

  protected onTypeChange(value: MemberType): void {
    this.addForm.update((f) => ({
      ...f,
      memberType: value,
      // Only adults can be invited; reset the toggle when switching away.
      sendInvitation: value === 'Adult' ? f.sendInvitation : false,
    }));
  }

  protected async submitAdd(): Promise<void> {
    if (this.submitting()) return;

    const f = this.addForm();
    if (f.displayName.trim().length === 0) {
      this.formError.set($localize`:@@nido.form.error.name-required:Necesitas un nombre.`);
      return;
    }
    if (!/^#[0-9a-fA-F]{6}$/.test(f.colorHex)) {
      this.formError.set($localize`:@@nido.form.error.color-format:El color debe tener formato #RRGGBB.`);
      return;
    }
    if (f.sendInvitation && f.contactEmail.trim().length === 0) {
      this.formError.set($localize`:@@nido.form.error.email-required:Para invitar por email necesitas un correo.`);
      return;
    }

    this.submitting.set(true);
    this.formError.set(null);
    try {
      if (f.sendInvitation) {
        const role: FamilyRole = f.inviteAsAdmin ? 'Admin' : 'Adult';
        const response = await firstValueFrom(this.invitationsApi.create({
          memberId: null,
          displayName: f.displayName.trim(),
          memberType: 'Adult',
          colorHex: f.colorHex,
          birthDate: f.birthDate || null,
          email: f.contactEmail.trim(),
          roleOnAccept: role,
        }));
        this.lastInvitation.set(response);
      } else {
        await firstValueFrom(this.api.create({
          displayName: f.displayName.trim(),
          memberType: f.memberType,
          colorHex: f.colorHex,
          birthDate: f.birthDate || null,
          contactEmail: f.contactEmail.trim() || null,
        }));
      }
      this.addOpen.set(false);
      await this.refreshAll();
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      this.formError.set(status === 400
        ? $localize`:@@nido.form.error.invalid-fields:Algún campo no es válido. Revisa el formulario.`
        : $localize`:@@nido.form.error.save-unknown:No se pudo guardar. Inténtalo de nuevo.`);
    } finally {
      this.submitting.set(false);
    }
  }

  // ─── per-member invite (existing adult, no account, no pending) ──────────

  protected canInvite(member: FamilyMember): boolean {
    return this.isAdmin()
      && member.memberType === 'Adult'
      && !member.hasAccount
      && member.pendingInvitation === null
      && member.isActive;
  }

  protected async inviteExisting(member: FamilyMember): Promise<void> {
    const email = member.contactEmail ?? window.prompt(`¿Email para invitar a ${member.displayName}?`)?.trim() ?? '';
    if (!email) return;

    try {
      const response = await firstValueFrom(this.invitationsApi.create({
        memberId: member.id,
        displayName: null,
        memberType: null,
        colorHex: null,
        birthDate: null,
        email,
        roleOnAccept: 'Adult',
      }));
      this.lastInvitation.set(response);
      await this.refreshAll();
    } catch {
      this.error.set('error_invite');
    }
  }

  // ─── invitations panel ──────────────────────────────────────────────────

  protected async copyLink(invitation: Invitation): Promise<void> {
    // We don't have the raw token after creation (server only returned the
    // copy link in CreateInvitationResponse). For invitations listed from a
    // refresh we don't have the URL — so this works only on the just-issued
    // invitation. Keep the bottom-banner alive while it's relevant.
    const last = this.lastInvitation();
    if (last && last.invitation.id === invitation.id) {
      await navigator.clipboard.writeText(last.copyLink).catch(() => undefined);
    }
  }

  protected async revoke(invitation: Invitation): Promise<void> {
    const msg = $localize`:@@nido.invitation.revoke-confirm:¿Revocar la invitación a ${invitation.email}:EMAIL:?`;
    if (!window.confirm(msg)) return;
    try {
      await firstValueFrom(this.invitationsApi.revoke(invitation.id));
      await this.refreshAll();
    } catch {
      this.error.set('error_revoke');
    }
  }

  /** "Resend" = revoke + create. Mints a fresh token for the same member. */
  protected async resend(invitation: Invitation): Promise<void> {
    try {
      await firstValueFrom(this.invitationsApi.revoke(invitation.id));
      const response = await firstValueFrom(this.invitationsApi.create({
        memberId: invitation.familyMemberId,
        displayName: null,
        memberType: null,
        colorHex: null,
        birthDate: null,
        email: invitation.email,
        roleOnAccept: invitation.roleOnAccept,
      }));
      this.lastInvitation.set(response);
      await this.refreshAll();
    } catch {
      this.error.set('error_resend');
    }
  }

  // ─── presentational helpers ─────────────────────────────────────────────

  protected memberSubtitle(member: FamilyMember): string {
    return memberSubtitle(member);
  }

  private emptyForm(): AddForm {
    return {
      displayName: '',
      memberType: 'Adult',
      colorHex: '#C96442',
      birthDate: '',
      contactEmail: '',
      sendInvitation: false,
      inviteAsAdmin: false,
    };
  }

  protected readField<K extends keyof AddForm>(field: K, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.addForm.update((f) => ({ ...f, [field]: value as AddForm[K] }));
  }
}
