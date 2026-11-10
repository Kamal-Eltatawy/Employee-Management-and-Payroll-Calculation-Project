import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  template: `
    <nav class="d-flex align-items-center justify-content-between flex-wrap gap-2" aria-label="Pagination">
      <span class="text-body-secondary small">
        @if (totalCount() === 0) {
          No results
        } @else {
          {{ firstItem() }}–{{ lastItem() }} of {{ totalCount() }}
        }
      </span>
      <ul class="pagination pagination-sm mb-0">
        <li class="page-item" [class.disabled]="page() <= 1">
          <button type="button" class="page-link" (click)="go(page() - 1)" [disabled]="page() <= 1">Previous</button>
        </li>
        <li class="page-item active" aria-current="page">
          <span class="page-link">{{ page() }} / {{ pageCount() }}</span>
        </li>
        <li class="page-item" [class.disabled]="page() >= pageCount()">
          <button type="button" class="page-link" (click)="go(page() + 1)" [disabled]="page() >= pageCount()">Next</button>
        </li>
      </ul>
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
