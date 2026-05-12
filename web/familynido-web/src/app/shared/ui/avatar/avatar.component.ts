import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';

/**
 * Circular avatar. When `photoUrl` is provided, renders the image cropped
 * into a circle; otherwise falls back to the member's initial drawn in
 * Fraunces over a soft tint of `colorHex`. The image fallback is
 * automatic — if the `<img>` fires `error` (404, network blip, …) the
 * component reverts to the initials version, so a stale `PhotoPath` never
 * breaks the layout.
 */
@Component({
  selector: 'fn-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './avatar.component.html',
  styleUrl: './avatar.component.css',
})
export class AvatarComponent {
  readonly name = input.required<string>();
  readonly colorHex = input<string | null>(null);
  readonly size = input<number>(36);
  readonly ring = input<boolean>(false);
  /** Absolute or relative URL to the avatar image. Null/empty → initials. */
  readonly photoUrl = input<string | null>(null);

  /** Cleared by the template when the <img> fires `error` so we degrade gracefully. */
  protected readonly imageBroken = signal(false);

  constructor() {
    // Reset the broken flag whenever the URL itself changes — a new upload
    // (cache-buster) should retry instead of staying stuck on initials.
    effect(() => {
      // Reading photoUrl() registers the dependency; the side effect
      // resets the broken flag whenever it changes.
      this.photoUrl();
      this.imageBroken.set(false);
    });
  }

  protected readonly initial = computed(() => (this.name()?.trim().charAt(0) ?? '?').toUpperCase());
  protected readonly fgColor = computed(() => this.colorHex() ?? '#C96442');

  /** Soft tint used as the avatar's background — 18% alpha over the fg color. */
  protected readonly bgColor = computed(() => {
    const hex = this.fgColor();
    return `${hex}2E`; // ~18% alpha
  });

  /** Subtle border when no ring is drawn. */
  protected readonly softBorder = computed(() => {
    const hex = this.fgColor();
    return `${hex}22`;
  });

  protected readonly showImage = computed(() => {
    const url = this.photoUrl();
    return !!url && !this.imageBroken();
  });

  protected onImageError(): void {
    this.imageBroken.set(true);
  }
}
