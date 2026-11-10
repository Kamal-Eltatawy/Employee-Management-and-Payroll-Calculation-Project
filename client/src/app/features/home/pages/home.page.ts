import { Component, LOCALE_ID, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { ApiError } from '../../../core/http/api-error';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { IconComponent } from '../../../shared/ui/icon.component';
import { toIsoMonth } from '../../../shared/util/dates';
import { formatMonth } from '../../../shared/util/months';
import { PayslipApiService } from '../../payslip/data/payslip-api.service';
import { Payslip } from '../../payslip/data/payslip.models';
import { DashboardApiService } from '../data/dashboard-api.service';
import { CountItem, Dashboard } from '../data/dashboard.models';

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

  protected readonly user = this.auth.currentUser;
  protected readonly isHr = this.auth.isHr;
  protected readonly monthLabel = formatMonth(toIsoMonth(new Date()), this.locale);

  protected readonly dashboard = signal<Dashboard | null>(null);
  protected readonly payslip = signal<Payslip | null>(null);
  protected readonly loading = signal(true);
  protected readonly message = signal<string | null>(null);

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

  constructor() {
    if (this.isHr()) {
      inject(DashboardApiService).get().subscribe({
        next: (dashboard) => {
          this.dashboard.set(dashboard);
          this.loading.set(false);
        },
        error: (error: ApiError) => this.fail(error),
      });
      return;
    }

    const employeeId = this.user()?.employeeId;
    if (employeeId) {
      inject(PayslipApiService).get(employeeId, toIsoMonth(new Date())).subscribe({
        next: (payslip) => {
          this.payslip.set(payslip);
          this.loading.set(false);
        },
        error: (error: ApiError) => this.fail(error),
      });
    }
  }

  protected barWidth(item: CountItem, items: CountItem[]): number {
    const max = Math.max(...items.map((i) => i.count), 1);
    return (item.count / max) * 100;
  }

  private fail(error: ApiError): void {
    this.message.set(error.detail ?? error.title);
    this.loading.set(false);
  }
}
