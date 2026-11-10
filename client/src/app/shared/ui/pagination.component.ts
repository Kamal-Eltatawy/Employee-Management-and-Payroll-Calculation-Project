import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  template: `
    <nav class="d-flex align-items-center justify-content-between flex-wrap gap-2" aria-label="Pagination"
         i18n-aria-label="@@pagination.label">
      <span class="text-body-secondary small">
        @if (totalCount() === 0) {
          <ng-container i18n="@@pagination.none">No results</ng-container>
        } @else {
          <ng-container i18n="@@pagination.range">{{ firstItem() }}–{{ lastItem() }} of {{ totalCount() }}</ng-container>
        }
      </span>
      <div class="d-flex align-items-center gap-2">
        <button type="button" class="btn btn-sm btn-outline-secondary" (click)="go(page() - 1)" [disabled]="page() <= 1"
                i18n="@@pagination.previous">Previous</button>
        <span class="small text-body-secondary" i18n="@@pagination.page">Page {{ page() }} of {{ pageCount() }}</span>
        <button type="button" class="btn btn-sm btn-outline-secondary" (click)="go(page() + 1)" [disabled]="page() >= pageCount()"
                i18n="@@pagination.next">Next</button>
      </div>
    </nav>
  `,
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly pageChange = output<number>();

  protected readonly pageCount = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  protected readonly firstItem = computed(() => (this.page() - 1) * this.pageSize() + 1);
  protected readonly lastItem = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  protected go(page: number): void {
    if (page >= 1 && page <= this.pageCount() && page !== this.page()) {
      this.pageChange.emit(page);
    }
  }
}
