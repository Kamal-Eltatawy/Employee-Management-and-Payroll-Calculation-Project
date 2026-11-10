import { Component, DestroyRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { BehaviorSubject, EMPTY, catchError, combineLatest, map, switchMap, tap } from 'rxjs';
import { ApiError } from '../../../core/http/api-error';
import { NotificationService } from '../../../core/notifications/notification.service';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { ConfirmDialogComponent } from '../../../shared/ui/confirm-dialog.component';
import { PaginationComponent } from '../../../shared/ui/pagination.component';
import { EmployeesApiService } from '../data/employees-api.service';
import {
  EmployeeListItem,
  EmployeeQuery,
  EmployeeSortBy,
  EmployeeStatus,
  PagedResult,
} from '../data/employee.models';
import { ReferenceDataApiService } from '../data/reference-data-api.service';

const DEFAULT_PAGE_SIZE = 10;

/** Reads the list state from the URL, so filters survive refresh and back/forward (D41). */
export function queryFromParams(params: ParamMap): EmployeeQuery {
  const positive = (value: string | null, fallback: number) => {
    const number = Number(value);
    return Number.isInteger(number) && number > 0 ? number : fallback;
  };
  const status = params.get('status');
  const sortBy = params.get('sortBy');

  return {
    search: params.get('search') ?? undefined,
    departmentId: positive(params.get('departmentId'), 0) || undefined,
    status: status === 'Active' || status === 'Inactive' ? status : undefined,
    sortBy: sortBy === 'hireDate' || sortBy === 'salary' ? sortBy : 'name',
    sortDir: params.get('sortDir') === 'desc' ? 'desc' : 'asc',
    page: positive(params.get('page'), 1),
    pageSize: Math.min(positive(params.get('pageSize'), DEFAULT_PAGE_SIZE), 100),
  };
}

@Component({
  selector: 'app-employee-list-page',
  imports: [RouterLink, MoneyPipe, PaginationComponent, ConfirmDialogComponent],
  templateUrl: './employee-list.page.html',
})
export class EmployeeListPage {
  private readonly api = inject(EmployeesApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly reload$ = new BehaviorSubject<void>(undefined);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  protected readonly departments = toSignal(inject(ReferenceDataApiService).departments(), { initialValue: [] });
  protected readonly query = signal<EmployeeQuery>(queryFromParams(this.route.snapshot.queryParamMap));
  protected readonly result = signal<PagedResult<EmployeeListItem> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    combineLatest([this.route.queryParamMap.pipe(map(queryFromParams)), this.reload$])
      .pipe(
        tap(([query]) => {
          this.query.set(query);
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(([query]) =>
          this.api.search(query).pipe(
            catchError((error: ApiError) => {
              this.error.set(error.detail ?? error.title);
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected applySearch(search: string): void {
    this.navigate({ search: search.trim() || null, page: 1 });
  }

  protected filterByDepartment(value: string): void {
    this.navigate({ departmentId: Number(value) || null, page: 1 });
  }

  protected filterByStatus(value: string): void {
    this.navigate({ status: (value as EmployeeStatus) || null, page: 1 });
  }

  protected sort(column: EmployeeSortBy): void {
    const current = this.query();
    const sortDir = current.sortBy === column && current.sortDir === 'asc' ? 'desc' : 'asc';
    this.navigate({ sortBy: column, sortDir, page: 1 });
  }

  protected sortIndicator(column: EmployeeSortBy): string {
    const current = this.query();
    return current.sortBy !== column ? '' : current.sortDir === 'asc' ? '▲' : '▼';
  }

  protected goToPage(page: number): void {
    this.navigate({ page });
  }

  protected async confirmDelete(employee: EmployeeListItem): Promise<void> {
    const confirmed = await this.confirmDialog().open(`Delete ${employee.fullName}? The employee will no longer appear in lists.`);
    if (!confirmed) {
      return;
    }

    this.api.delete(employee.id).subscribe({
      next: () => {
        this.notifications.success(`${employee.fullName} was deleted.`);
        this.reload$.next();
      },
      error: (error: ApiError) => this.notifications.error(error.detail ?? error.title),
    });
  }

  private navigate(changes: Record<string, string | number | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: changes, queryParamsHandling: 'merge' });
  }
}
