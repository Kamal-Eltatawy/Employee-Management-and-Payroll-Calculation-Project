import { Component, inject, input } from '@angular/core';
import { FormArray, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { maxTwoDecimals } from '../../../shared/forms/validators';
import { FieldErrorsComponent } from '../../../shared/ui/field-errors.component';
import { PayComponentType } from '../data/employee.models';
import { PayComponentForm } from './pay-component-form';

/** Edits the employee's allowances and deductions (one FormArray, D44). */
@Component({
  selector: 'app-pay-components-editor',
  imports: [ReactiveFormsModule, FieldErrorsComponent],
  template: `
    <fieldset>
      <legend class="h6 d-flex align-items-center justify-content-between">
        <span>Allowances &amp; deductions</span>
        <button type="button" class="btn btn-sm btn-outline-primary" (click)="add()">Add item</button>
      </legend>

      @if (array().length === 0) {
        <p class="text-body-secondary small mb-0">No allowances or deductions.</p>
      }

      @for (row of array().controls; track row; let i = $index) {
        <div class="row g-2 align-items-start mb-2" [formGroup]="row">
          <div class="col-md-6">
            <label class="form-label visually-hidden" [for]="'type' + i">Type</label>
            <select class="form-select" [id]="'type' + i" formControlName="payComponentTypeId"
                    [class.is-invalid]="isDuplicate(i) || (row.controls.payComponentTypeId.invalid && row.controls.payComponentTypeId.touched)">
              <option [ngValue]="0" disabled>Select a type…</option>
              @for (type of types(); track type.id) {
                <option [ngValue]="type.id">
                  {{ type.name }} ({{ type.kind === 'Allowance' ? (type.isTaxable ? 'taxable allowance' : 'non-taxable allowance') : 'deduction' }})
                </option>
              }
            </select>
            @if (isDuplicate(i)) {
              <div class="invalid-feedback d-block">This type is already in the list.</div>
            }
            <app-field-errors [control]="row.controls.payComponentTypeId" label="Type" />
          </div>
          <div class="col-md-4">
            <label class="form-label visually-hidden" [for]="'amount' + i">Amount</label>
            <input class="form-control" type="number" min="0.01" step="0.01" [id]="'amount' + i" formControlName="amount"
                   [class.is-invalid]="row.controls.amount.invalid && row.controls.amount.touched" />
            <app-field-errors [control]="row.controls.amount" label="Amount" />
          </div>
          <div class="col-md-2">
            <button type="button" class="btn btn-outline-danger w-100" (click)="remove(i)">Remove</button>
          </div>
        </div>
      }
    </fieldset>
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
