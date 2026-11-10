import { Component, DestroyRef, LOCALE_ID, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ApiError } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { IconComponent } from '../../../shared/ui/icon.component';
import { MonthPickerComponent } from '../../../shared/ui/month-picker.component';
import { formatMonth } from '../../../shared/util/months';
import { toIsoMonth } from '../../../shared/util/dates';
import { PayslipApiService } from '../data/payslip-api.service';
import { Payslip } from '../data/payslip.models';

const MONTH_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;

/**
 * Payslip for one employee and month. The month lives in the URL (?month=yyyy-MM); default: the current month.
 * Used by Hr (/employees/:id/payslip) and by an Employee for themself (/my-payslip, id from the token).
 */
@Component({
  selector: 'app-payslip-page',
  imports: [RouterLink, MoneyPipe, IconComponent, MonthPickerComponent],
  templateUrl: './payslip.page.html',
  styleUrl: './payslip.page.scss',
})
export class PayslipPage {
  /** Route parameter and query parameter, bound by withComponentInputBinding. */
  readonly id = input<string>();
  readonly month = input<string>();

  private readonly api = inject(PayslipApiService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  protected readonly isHr = this.auth.isHr;
  private readonly locale = inject(LOCALE_ID);
  private readonly employeeId = computed(() => Number(this.id() ?? this.auth.currentUser()?.employeeId ?? 0));

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
    toObservable(computed(() => ({ id: this.employeeId(), month: this.selectedMonth() })))
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

  /** "2026-11" → "November 2026" / "نوفمبر 2026", Latin digits in every language. */
  protected readonly monthLabel = computed(() => formatMonth(this.selectedMonth(), this.locale));

  protected readonly totalDeductions = computed(() => {
    const slip = this.payslip();
    return slip ? slip.incomeTax + slip.socialInsurance + slip.otherDeductionsTotal : 0;
  });

  protected methodLabel(method: string): string {
    switch (method) {
      case 'Progressive':
        return $localize`:@@taxMethod.progressive:Progressive brackets`;
      case 'Flat':
        return $localize`:@@taxMethod.flat:Flat rate`;
      default:
        return $localize`:@@taxMethod.exempt:Tax exempt`;
    }
  }

  protected print(): void {
    window.print();
  }

  protected changeMonth(month: string): void {
    if (MONTH_PATTERN.test(month)) {
      void this.router.navigate([], { queryParams: { month }, queryParamsHandling: 'merge' });
    }
  }
}
