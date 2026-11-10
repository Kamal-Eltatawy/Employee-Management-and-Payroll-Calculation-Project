import { Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, switchMap, tap } from 'rxjs';
import { ApiError } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { toIsoMonth } from '../../../shared/util/dates';
import { PayslipApiService } from '../data/payslip-api.service';
import { Payslip } from '../data/payslip.models';

const MONTH_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;

/** Payslip for one employee and month. The month lives in the URL (?month=yyyy-MM); default: the current month. */
@Component({
  selector: 'app-payslip-page',
  imports: [RouterLink, MoneyPipe],
  templateUrl: './payslip.page.html',
  styleUrl: './payslip.page.scss',
})
export class PayslipPage {
  /** Route parameter and query parameter, bound by withComponentInputBinding. */
  readonly id = input.required<string>();
  readonly month = input<string>();

  private readonly api = inject(PayslipApiService);
  private readonly router = inject(Router);

  protected readonly selectedMonth = computed(() => {
    const month = this.month();
    return month && MONTH_PATTERN.test(month) ? month : toIsoMonth(new Date());
  });
  protected readonly payslip = signal<Payslip | null>(null);
  protected readonly loading = signal(true);
  /** 422 (inactive, before hire, no policy) is a business answer, shown as a warning. */
  protected readonly warning = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    toObservable(computed(() => ({ id: Number(this.id()), month: this.selectedMonth() })))
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.warning.set(null);
          this.error.set(null);
        }),
        switchMap(({ id, month }) =>
          this.api.get(id, month).pipe(
            catchError((error: ApiError) => {
              this.payslip.set(null);
              (error.status === 422 ? this.warning : this.error).set(error.detail ?? error.title);
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((payslip) => {
        this.payslip.set(payslip);
        this.loading.set(false);
      });
  }

  protected changeMonth(month: string): void {
    if (MONTH_PATTERN.test(month)) {
      void this.router.navigate([], { queryParams: { month }, queryParamsHandling: 'merge' });
    }
  }
}
