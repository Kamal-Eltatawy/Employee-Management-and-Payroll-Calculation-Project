import { Component, LOCALE_ID, computed, inject, input, viewChild } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError, inlineMessage, resourceApiError } from '../../../core/http/api-error';
import { NotificationService } from '../../../core/notifications/notification.service';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { ConfirmDialogComponent } from '../../../shared/ui/confirm-dialog.component';
import { IconComponent } from '../../../shared/ui/icon.component';
import { toIsoMonth } from '../../../shared/util/dates';
import { formatMonth } from '../../../shared/util/months';
import { ReferenceDataApiService } from '../../employees/data/reference-data-api.service';
import { PayrollPoliciesApiService } from '../data/payroll-policies-api.service';
import { PayrollPolicy } from '../data/payroll-policy.models';
import { formatPercent, taxMethodLabel } from '../ui/tax-brackets';

/** The policy in force in a month: the latest one that starts on or before it. */
export function policyInEffect(policies: readonly PayrollPolicy[], month: string): PayrollPolicy | null {
  const firstDay = `${month}-01`;
  return policies
    .filter((policy) => policy.effectiveFrom <= firstDay)
    .reduce<PayrollPolicy | null>((latest, policy) => (!latest || policy.effectiveFrom > latest.effectiveFrom ? policy : latest), null);
}

/** Effective-dated payroll policies, one tab per tax scheme (the scheme lives in the URL: ?scheme=id). */
@Component({
  selector: 'app-payroll-policy-list-page',
  imports: [RouterLink, MoneyPipe, ConfirmDialogComponent, IconComponent],
  templateUrl: './payroll-policy-list.page.html',
  styleUrl: './payroll-policy-list.page.scss',
})
export class PayrollPolicyListPage {
  /** Query parameter, bound by withComponentInputBinding. */
  readonly scheme = input<string>();

  private readonly api = inject(PayrollPoliciesApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly locale = inject(LOCALE_ID);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  protected readonly schemes = toSignal(inject(ReferenceDataApiService).taxSchemes(), { initialValue: [] });
  private readonly policies = rxResource({ stream: () => this.api.list() });

  protected readonly selectedScheme = computed(() => {
    const schemes = this.schemes();
    const id = Number(this.scheme());
    return schemes.find((scheme) => scheme.id === id) ?? (schemes.length > 0 ? schemes[0] : null);
  });

  /** Newest first, so upcoming policies are on top. */
  protected readonly schemePolicies = computed(() => {
    const scheme = this.selectedScheme();
    const all = this.policies.hasValue() ? this.policies.value() : [];
    return all
      .filter((policy) => policy.taxSchemeId === scheme?.id)
      .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom));
  });

  private readonly currentMonth = toIsoMonth(new Date());
  protected readonly inEffectId = computed(() => policyInEffect(this.schemePolicies(), this.currentMonth)?.id ?? null);
  protected readonly loading = this.policies.isLoading;
  /** 4xx only: network failures and 5xx are already shown as a toast by problemDetailsInterceptor. */
  protected readonly error = computed(() => inlineMessage(resourceApiError(this.policies.error())));

  protected readonly methodLabel = taxMethodLabel;

  protected selectScheme(id: number): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { scheme: id }, queryParamsHandling: 'merge' });
  }

  protected month(policy: PayrollPolicy): string {
    return formatMonth(policy.effectiveFrom.slice(0, 7), this.locale);
  }

  protected isUpcoming(policy: PayrollPolicy): boolean {
    return policy.effectiveFrom.slice(0, 7) > this.currentMonth;
  }

  protected percent(value: number): string {
    return formatPercent(value, this.locale);
  }

  protected taxSummary(policy: PayrollPolicy): string {
    const brackets = policy.taxBrackets;
    switch (this.selectedScheme()?.method) {
      case 'Exempt':
        return $localize`:@@policies.noIncomeTax:No income tax`;
      case 'Flat':
        return brackets.length > 0 ? this.percent(brackets[0].ratePercent) : '';
      default: {
        const count = brackets.length;
        const top = brackets.length > 0 ? this.percent(Math.max(...brackets.map((bracket) => bracket.ratePercent))) : '';
        return $localize`:@@policies.bracketSummary:${count}:count: brackets, top rate ${top}:top:`;
      }
    }
  }

  protected async confirmDelete(policy: PayrollPolicy): Promise<void> {
    const month = this.month(policy);
    const confirmed = await this.confirmDialog().open(
      $localize`:@@policies.deleteConfirm:Delete the policy effective from ${month}:month:? Months it covered fall back to the previous policy.`,
    );
    if (!confirmed) {
      return;
    }

    this.api.delete(policy.id).subscribe({
      next: () => {
        this.notifications.success($localize`:@@policies.deleted:The policy effective from ${month}:month: was deleted.`);
        this.policies.reload();
      },
      error: (error: ApiError) => this.notifications.error(error.detail ?? error.title),
    });
  }
}
