import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { IconComponent } from '../icon/icon.component';

/** Sentinel used in the page-items list to render an ellipsis between numbers. */
type PageItem = number | 'ellipsis';

/**
 * Numbered pagination control. Stateless: parent owns `currentPage` and
 * receives `pageChange` events. Renders «‹» previous, a windowed list of
 * page numbers with ellipses when there are too many to show, and «›» next.
 */
@Component({
  selector: 'fn-pagination',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent],
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.css',
})
export class PaginationComponent {
  readonly currentPage = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageChange = output<number>();

  protected readonly previousAriaLabel = $localize`:@@pagination.previous:Página anterior`;
  protected readonly nextAriaLabel = $localize`:@@pagination.next:Página siguiente`;

  protected pageAriaLabel(page: number): string {
    return $localize`:@@pagination.page-aria:Ir a la página ${page}:PAGE:`;
  }

  /**
   * Compact view of page numbers: under 7 pages all numbers; otherwise the
   * first, the last, the current ±1, and ellipses where there's a gap.
   */
  protected readonly pageItems = computed<PageItem[]>(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }

    const items: PageItem[] = [1];
    if (current > 4) items.push('ellipsis');

    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);
    for (let i = start; i <= end; i++) {
      items.push(i);
    }

    if (current < total - 3) items.push('ellipsis');
    items.push(total);
    return items;
  });

  protected readonly canGoPrevious = computed(() => this.currentPage() > 1);
  protected readonly canGoNext = computed(() => this.currentPage() < this.totalPages());

  protected goTo(item: PageItem): void {
    if (item === 'ellipsis') return;
    if (item < 1 || item > this.totalPages() || item === this.currentPage()) return;
    this.pageChange.emit(item);
  }

  protected previous(): void {
    if (this.canGoPrevious()) this.pageChange.emit(this.currentPage() - 1);
  }

  protected next(): void {
    if (this.canGoNext()) this.pageChange.emit(this.currentPage() + 1);
  }

  /** Type-narrowing helper used in the template. */
  protected isNumber(item: PageItem): item is number {
    return typeof item === 'number';
  }
}
