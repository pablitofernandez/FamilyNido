import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

/**
 * Friendly stop page shown when the authenticated user has no linked
 * {@link FamilyMember}. Prompts the admin to link them, per RF-AUTH-003.
 */
@Component({
  selector: 'fn-not-linked',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './not-linked.component.html',
  styleUrl: './not-linked.component.css',
})
export class NotLinkedComponent {
  private readonly auth = inject(AuthService);

  protected logout(): void {
    void this.auth.logout();
  }
}
