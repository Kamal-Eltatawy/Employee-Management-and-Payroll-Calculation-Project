import { Component, LOCALE_ID, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { inlineMessage, resourceApiError } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { IconComponent } from '../../../shared/ui/icon.component';
import { toIsoMonth } from '../../../shared/util/dates';
import { formatMonth } from '../../../shared/util/months';
import { PayslipApiService } from '../../payslip/data/payslip-api.service';
import { DashboardApiService } from '../data/dashboard-api.service';
import { CountItem } from '../data/dashboard.models';

/** Home page (D59): figures and the month's payroll for HR; the current payslip summary for an employee. */
@Component({
  selector: 'app-home-page',
  imports: [RouterLink, MoneyPipe, IconComponent],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage {
  private readonly auth = inject(AuthService);
  private readonly locale = inject(LOCALE_ID);
  private readonly dashboardApi = inject(DashboardApiService);
  private readonly payslipApi = inject(PayslipApiService);

  protected readonly user = this.auth.currentUser;
  protected readonly isHr = this.auth.isHr;
  protected readonly monthLabel = formatMonth(toIsoMonth(new Date()), this.locale);

  private readonly month = toIsoMonth(new Date());
  /** The employee record of the signed-in user; Hr wins, so an Hr + Employee user gets the dashboard (D67). */
  private readonly ownEmployeeId = computed(() => (this.isHr() ? null : (this.user()?.employeeId ?? null)));
  /** An Employee account that is not linked to an employee record: nothing to load (item 11). */
  protected readonly noEmployeeRecord = computed(() => !this.isHr() && this.ownEmployeeId() === null);

  private readonly dashboardResource = rxResource({
    params: () => (this.isHr() ? true : undefined),
    stream: () => this.dashboardApi.get(),
  });
  private readonly payslipResource = rxResource({
    params: () => this.ownEmployeeId() ?? undefined,
    stream: ({ params }) => this.payslipApi.get(params, this.month),
  });

  protected readonly dashboard = computed(() => (this.dashboardResource.hasValue() ? this.dashboardResource.value() ?? null : null));
  protected readonly payslip = computed(() => (this.payslipResource.hasValue() ? this.payslipResource.value() ?? null : null));
  protected readonly loading = computed(() => this.dashboardResource.isLoading() || this.payslipResource.isLoading());
  private readonly failure = computed(
    () => resourceApiError(this.dashboardResource.error()) ?? resourceApiError(this.payslipResource.error()),
  );
  protected readonly failed = computed(() => this.failure() !== null);
  /** 4xx (and 422) inline; network failures and 5xx are already a toast (problemDetailsInterceptor). */
  protected readonly message = computed(() => inlineMessage(this.failure()));

  /** Share of gross pay for each part of the stacked bar. */
  protected readonly breakdown = computed(() => {
    const payroll = this.dashboard()?.payroll;
    if (!payroll || payroll.grossPay <= 0) {
      return [];
    }

    const share = (value: number) => Math.max(0, (value / payroll.grossPay) * 100);
    return [
      { key: 'net', label: $localize`:@@payslip.net:Net pay`, value: payroll.netPay, share: share(payroll.netPay) },
      { key: 'tax', label: $localize`:@@payslip.incomeTax:Income tax`, value: payroll.incomeTax, share: share(payroll.incomeTax) },
      {
        key: 'insurance',
        label: $localize`:@@payslip.socialInsurance:Social insurance`,
        value: payroll.socialInsurance,
        share: share(payroll.socialInsurance),
      },
      {
        key: 'other',
        label: $localize`:@@home.otherDeductions:Other deductions`,
        value: payroll.otherDeductions,
        share: share(payroll.otherDeductions),
      },
    ];
  });

  protected barWidth(item: CountItem, items: CountItem[]): number {
    const max = Math.max(...items.map((i) => i.count), 1);
    return (item.count / max) * 100;
  }
}
