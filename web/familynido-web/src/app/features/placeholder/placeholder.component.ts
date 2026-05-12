import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Full-screen "coming soon" placeholder used for routes whose feature is
 * scheduled in a later phase (Calendar, Wall, Meals in Phase 1–3).
 */
@Component({
  selector: 'fn-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './placeholder.component.html',
  styleUrl: './placeholder.component.css',
})
export class PlaceholderComponent {
  readonly title = input.required<string>();
  readonly tagline = input<string>('Pronto en el nido.');
  readonly phase = input<string>('una fase posterior');
}
