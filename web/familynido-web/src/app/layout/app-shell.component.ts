import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../core/auth/auth.service';
import { AvatarComponent } from '../shared/ui/avatar/avatar.component';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { NAV_TABS } from './nav-tabs';

/**
 * Primary app shell: top navigation on desktop, bottom "liquid warm" tab bar
 * on mobile. Content mounts inside a central `<router-outlet>`. The shell is
 * behind the auth guard, so it always renders with a known user.
 */
@Component({
  selector: 'fn-app-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, IconComponent, AvatarComponent],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.css',
})
export class AppShellComponent {
  private readonly auth = inject(AuthService);

  protected readonly tabs = NAV_TABS;
  protected readonly me = this.auth.me;
  protected readonly displayName = computed(() => this.me()?.displayName ?? '');
  protected readonly colorHex = computed(() => this.me()?.colorHex ?? '#C96442');
  protected readonly familyName = computed(() => this.me()?.familyName ?? 'FamilyNido');
  protected readonly photoUrl = computed(() => {
    const me = this.me();
    return me?.photoPath && me.memberId
      ? `/api/family-members/${me.memberId}/photo`
      : null;
  });

  protected logout(): void {
    void this.auth.logout();
  }
}
