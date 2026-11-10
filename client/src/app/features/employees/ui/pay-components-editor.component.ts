import { Component, inject, input } from '@angular/core';
import { FormArray, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { maxTwoDecimals } from '../../../shared/forms/validators';
import { FieldErrorsComponent } from '../../../shared/ui/field-errors.component';
import { IconComponent } from '../../../shared/ui/icon.component';
import { PayComponentType } from '../data/employee.models';
import { PayComponentForm } from './pay-component-form';

/** Edits the employee's allowances and deductions (one FormArray, D44). */
@Component({
  selector: 'app-pay-components-editor',
  imports: [ReactiveFormsModule, FieldErrorsComponent, IconComponent],
  template: `
    <div class="editor">
      <div class="editor-head">
        <span class="form-label mb-0" i18n="@@payComponents.title">Allowances &amp; deductions</span>
        <button type="button" class="btn btn-sm btn-outline-primary d-inline-flex align-items-center gap-1" (click)="add()">
          <app-icon name="plus" [size]="16" />
          <span i18n="@@payComponents.add">Add item</span>
        </button>
      </div>

      @if (array().length === 0) {
        <p class="empty" i18n="@@payComponents.empty">No allowances or deductions.</p>
      }

      @for (row of array().controls; track row; let i = $index) {
        <div class="item" [formGroup]="row">
          <div class="type">
            <label class="visually-hidden" [for]="'type' + i" i18n="@@field.type">Type</label>
            <select class="form-select" [id]="'type' + i" formControlName="payComponentTypeId"
                    [class.is-invalid]="isDuplicate(i) || (row.controls.payComponentTypeId.invalid && row.controls.payComponentTypeId.touched)">
              <option [ngValue]="0" disabled i18n="@@payComponents.selectType">Select a type…</option>
              @for (type of types(); track type.id) {
                <option [ngValue]="type.id">{{ type.name }} · {{ describe(type) }}</option>
              }
            </select>
            @if (isDuplicate(i)) {
              <div class="invalid-feedback d-block" i18n="@@payComponents.duplicate">This type is already in the list.</div>
            }
            <app-field-errors [control]="row.controls.payComponentTypeId" label="Type" i18n-label="@@field.type" />
          </div>
          <div class="amount-field">
            <label class="visually-hidden" [for]="'amount' + i" i18n="@@field.amount">Amount</label>
            <input class="form-control" type="number" min="0.01" step="0.01" [id]="'amount' + i" formControlName="amount"
                   [class.is-invalid]="row.controls.amount.invalid && row.controls.amount.touched" />
            <app-field-errors [control]="row.controls.amount" label="Amount" i18n-label="@@field.amount" />
          </div>
          <button type="button" class="btn btn-light icon-button text-danger" (click)="remove(i)"
                  title="Remove" i18n-title="@@payComponents.remove" aria-label="Remove" i18n-aria-label="@@payComponents.remove">
            <app-icon name="trash" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .editor { border: 1px dashed var(--app-border); border-radius: .75rem; padding: 1rem; background: #fcfcfd; }
    .editor-head { display: flex; align-items: center; justify-content: space-between; margin-block-end: .75rem; }
    .empty { color: var(--app-muted); font-size: .9rem; margin: 0; }
    .item { display: grid; grid-template-columns: minmax(0, 1fr) 180px auto; gap: .5rem; align-items: start; margin-block-end: .5rem; }
    @media (max-width: 575.98px) { .item { grid-template-columns: 1fr; } }
  `,
})
export class PayComponentsEditorComponent {
  readonly array = input.required<FormArray<PayComponentForm>>();
  readonly types = input.required<PayComponentType[]>();

  private readonly fb = inject(NonNullableFormBuilder);

  static createRow(fb: NonNullableFormBuilder, typeId = 0, amount = 0): PayComponentForm {
    return fb.group({
      payComponentTypeId: fb.control(typeId, [Validators.min(1)]),
      amount: fb.control(amount, [Validators.required, Validators.min(0.01), maxTwoDecimals()]),
    });
  }

  protected describe(type: PayComponentType): string {
    if (type.kind === 'Deduction') {
      return $localize`:@@payComponents.kindDeduction:deduction`;
    }

    return type.isTaxable
      ? $localize`:@@payComponents.kindTaxable:taxable allowance`
      : $localize`:@@payComponents.kindNonTaxable:non-taxable allowance`;
  }

  protected add(): void {
    this.array().push(PayComponentsEditorComponent.createRow(this.fb));
  }

  protected remove(index: number): void {
    this.array().removeAt(index);
  }

  protected isDuplicate(index: number): boolean {
    const duplicates = this.array().errors?.['duplicateType'] as number[] | undefined;
    return duplicates?.includes(index) ?? false;
  }
}
