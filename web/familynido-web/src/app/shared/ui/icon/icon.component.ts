import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { IconName, Icons } from './icons';

/**
 * Line SVG icon. Accepts either a preset {@link IconName} (resolves to a path
 * from {@link Icons}) or an explicit `d` attribute for one-offs. Stroke width
 * defaults to 1.75 to match the "Editorial Cálido" line style.
 */
@Component({
  selector: 'fn-icon',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.css',
})
export class IconComponent {
  readonly name = input<IconName | null>(null);
  readonly d = input<string | null>(null);
  readonly size = input<number>(20);
  readonly strokeWidth = input<number>(1.75);

  protected readonly resolvedPath = (): string => {
    const named = this.name();
    if (named) return Icons[named];
    return this.d() ?? '';
  };
}
