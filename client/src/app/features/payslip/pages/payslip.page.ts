import { Component, LOCALE_ID, computed, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { inlineMessage, resourceApiError } from '../../../core/http/api-error';
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

  /** /employees/:id/payslip is the Hr view; /my-payslip is the signed-in employee's own payslip. */
  protected readonly isOwn = computed(() => !this.id());
  private readonly locale = inject(LOCALE_ID);
  /** Null when the account is not linked to an employee record (item 11): nothing is requested. */
  private readonly employeeId = computed(() => {
    const id = Number(this.id() ?? this.auth.currentUser()?.employeeId ?? 0);
    return Number.isInteger(id) && id > 0 ? id : null;
  });
  protected readonly noEmployeeRecord = computed(() => this.employeeId() === null);

  protected readonly selectedMonth = computed(() => {
    const month = this.month();
    return month && MONTH_PATTERN.test(month) ? month : toIsoMonth(new Date());
  });

  private readonly slip = rxResource({
    params: () => {
      const id = this.employeeId();
      return id === null ? undefined : { id, month: this.selectedMonth() };
    },
    stream: ({ params }) => this.api.get(params.id, params.month),
  });

  private readonly failure = computed(() => resourceApiError(this.slip.error()));
  /** The shown payslip: kept while another month loads, cleared when a request fails (as before). */
  protected readonly payslip = linkedSignal<{ value: Payslip | undefined; failed: boolean }, Payslip | null>({
    source: () => ({
      value: this.slip.hasValue() ? this.slip.value() : undefined,
      failed: this.failure() !== null || this.noEmployeeRecord(),
    }),
    computation: ({ value, failed }, previous) => value ?? (failed ? null : (previous?.value ?? null)),
  });
  protected readonly loading = this.slip.isLoading;
  /** 422 (inactive, before hire, no policy) is a business answer, shown as a warning. */
  protected readonly warning = computed(() => {
    const failure = this.failure();
    return failure?.status === 422 ? (failure.detail ?? failure.title) : null;
  });
  /** Other 4xx inline; network failures and 5xx are already a toast (problemDetailsInterceptor). */
  protected readonly error = computed(() => {
    const failure = this.failure();
    return failure?.status === 422 ? null : inlineMessage(failure);
  });

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
