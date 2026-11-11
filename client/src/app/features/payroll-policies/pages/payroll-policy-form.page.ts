import { Component, LOCALE_ID, OnInit, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiError } from '../../../core/http/api-error';
import { NotificationService } from '../../../core/notifications/notification.service';
import { applyServerErrors } from '../../../shared/forms/apply-server-errors';
import { maxTwoDecimals, nonNegative, notBeforeCurrentMonth, percentRange, requiredSelection } from '../../../shared/forms/validators';
import { FieldErrorsComponent } from '../../../shared/ui/field-errors.component';
import { IconComponent } from '../../../shared/ui/icon.component';
import { MonthPickerComponent } from '../../../shared/ui/month-picker.component';
import { toIsoMonth } from '../../../shared/util/dates';
import { formatMonth } from '../../../shared/util/months';
import { ReferenceDataApiService } from '../../employees/data/reference-data-api.service';
import { PayrollPoliciesApiService } from '../data/payroll-policies-api.service';
import { PayrollPolicy, PayrollPolicyRequest, TaxMethod } from '../data/payroll-policy.models';
import { TaxBracketsEditorComponent } from '../ui/tax-brackets-editor.component';
import { TaxBracketForm, createBracketRow, taxBracketsValidator, taxMethodLabel, toTaxBrackets } from '../ui/tax-brackets';

function nextMonth(): string {
  const today = new Date();
  return toIsoMonth(new Date(today.getFullYear(), today.getMonth() + 1, 1));
}

/**
 * Create, edit or view one payroll policy. Policies that started before the current month cover closed months
 * and are read-only (D54). Validators mirror the server rules; the server stays the authority.
 */
@Component({
  selector: 'app-payroll-policy-form-page',
  imports: [ReactiveFormsModule, RouterLink, FieldErrorsComponent, IconComponent, MonthPickerComponent, TaxBracketsEditorComponent],
  templateUrl: './payroll-policy-form.page.html',
  styleUrl: '../../employees/pages/employee-form.page.scss',
})
export class PayrollPolicyFormPage implements OnInit {
  /** Route parameter and query parameter (?scheme=id on create), bound by withComponentInputBinding. */
  readonly id = input<string>();
  readonly scheme = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(PayrollPoliciesApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly locale = inject(LOCALE_ID);

  protected readonly schemes = toSignal(inject(ReferenceDataApiService).taxSchemes(), { initialValue: [] });

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  /** The policy covers closed months: shown, not editable. */
  protected readonly readOnly = signal(false);

  /** Declared before the form: the brackets validator reads the method while the form is being built. */
  private readonly schemeId = signal(0);
  protected readonly method = computed<TaxMethod | undefined>(
    () => this.schemes().find((scheme) => scheme.id === this.schemeId())?.method,
  );

  protected readonly form = this.fb.group({
    taxSchemeId: [0, [requiredSelection()]],
    /** yyyy-MM in the form; the request carries the first day of the month. */
    effectiveFrom: [nextMonth(), [Validators.required, notBeforeCurrentMonth()]],
    insuranceRatePercent: this.fb.control<number | null>(null, [Validators.required, percentRange(), maxTwoDecimals()]),
    /** Empty = no cap; 0 is a real cap. */
    insuranceCap: this.fb.control<number | null>(null, [nonNegative(), maxTwoDecimals()]),
    taxBrackets: this.fb.array<TaxBracketForm>([], { validators: taxBracketsValidator(() => this.method()) }),
  });

  protected readonly methodLabel = taxMethodLabel;

  constructor() {
    this.form.controls.taxSchemeId.valueChanges.pipe(takeUntilDestroyed()).subscribe((id) => this.schemeId.set(id));
    // The bracket rules depend on the scheme's method: re-check them, and give the rows the shape the method needs.
    effect(() => {
      const method = this.method();
      untracked(() => this.fitRowsTo(method));
    });
  }

  protected get isEdit(): boolean {
    return !!this.id();
  }

  ngOnInit(): void {
    const id = Number(this.id());
    if (!id) {
      const schemeId = Number(this.scheme());
      if (Number.isInteger(schemeId) && schemeId > 0) {
        this.form.controls.taxSchemeId.setValue(schemeId);
      }
      return;
    }

    // A policy cannot move to another scheme.
    this.form.controls.taxSchemeId.disable({ emitEvent: false });
    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (policy) => {
        this.fill(policy);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.formError.set(error.detail ?? error.title);
        this.loading.set(false);
      },
    });
  }

  protected monthLabel(): string {
    return formatMonth(this.form.controls.effectiveFrom.value, this.locale);
  }

  protected changeMonth(month: string): void {
    const control = this.form.controls.effectiveFrom;
    control.setValue(month);
    control.markAsTouched();
  }

  protected save(): void {
    this.formError.set(null);
    if (this.readOnly()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: PayrollPolicyRequest = {
      taxSchemeId: value.taxSchemeId,
      effectiveFrom: `${value.effectiveFrom}-01`,
      insuranceRatePercent: Number(value.insuranceRatePercent),
      insuranceCap: typeof value.insuranceCap === 'number' ? value.insuranceCap : null,
      taxBrackets: toTaxBrackets(value.taxBrackets),
    };
    const id = Number(this.id());
    const save$: Observable<PayrollPolicy> = id ? this.api.update(id, request) : this.api.create(request);

    this.saving.set(true);
    save$.subscribe({
      next: (policy) => {
        this.saving.set(false);
        const month = formatMonth(policy.effectiveFrom.slice(0, 7), this.locale);
        this.notifications.success($localize`:@@policies.saved:The policy effective from ${month}:month: was saved.`);
        void this.router.navigate(['/payroll-policies'], { queryParams: { scheme: policy.taxSchemeId } });
      },
      error: (error: ApiError) => {
        this.saving.set(false);
        const placedOnFields = (error.status === 400 || error.status === 409) && applyServerErrors(this.form, error);
        if (!placedOnFields) {
          this.formError.set(error.detail ?? error.title);
        }
      },
    });
  }

  /** Exempt: no rows; Flat: exactly one open row (its rate is kept); Progressive: at least one row to start from. */
  private fitRowsTo(method: TaxMethod | undefined): void {
    const rows = this.form.controls.taxBrackets;
    if (!this.readOnly()) {
      if (method === 'Exempt' && rows.length > 0) {
        rows.clear();
      } else if (method === 'Flat' && (rows.length !== 1 || rows.at(0).controls.upperBound.value !== null)) {
        const rate = rows.length > 0 ? rows.at(rows.length - 1).controls.ratePercent.value : null;
        rows.clear();
        rows.push(createBracketRow(this.fb, null, rate));
      } else if (method === 'Progressive' && rows.length === 0) {
        rows.push(createBracketRow(this.fb));
      }
    }

    rows.updateValueAndValidity();
  }

  private fill(policy: PayrollPolicy): void {
    const rows = this.form.controls.taxBrackets;
    rows.clear({ emitEvent: false });
    for (const bracket of policy.taxBrackets) {
      rows.push(createBracketRow(this.fb, bracket.upperBound, bracket.ratePercent), { emitEvent: false });
    }

    this.form.patchValue({
      taxSchemeId: policy.taxSchemeId,
      effectiveFrom: policy.effectiveFrom.slice(0, 7),
      insuranceRatePercent: policy.insuranceRatePercent,
      insuranceCap: policy.insuranceCap,
    });

    if (!policy.isModifiable) {
      this.readOnly.set(true);
      this.form.disable();
    }
  }
}
