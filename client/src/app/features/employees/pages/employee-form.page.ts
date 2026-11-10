import { Component, OnInit, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiError } from '../../../core/http/api-error';
import { NotificationService } from '../../../core/notifications/notification.service';
import { applyServerErrors } from '../../../shared/forms/apply-server-errors';
import { maxTwoDecimals, noDuplicateType, notInFuture } from '../../../shared/forms/validators';
import { FieldErrorsComponent } from '../../../shared/ui/field-errors.component';
import { IconComponent } from '../../../shared/ui/icon.component';
import { EmployeesApiService } from '../data/employees-api.service';
import { Employee, EmployeeRequest, EmployeeStatus } from '../data/employee.models';
import { ReferenceDataApiService } from '../data/reference-data-api.service';
import { PayComponentForm } from '../ui/pay-component-form';
import { PayComponentsEditorComponent } from '../ui/pay-components-editor.component';

/** One page for create and edit (D44). Validators mirror the server rules (§10); the server stays the authority. */
@Component({
  selector: 'app-employee-form-page',
  imports: [ReactiveFormsModule, RouterLink, FieldErrorsComponent, PayComponentsEditorComponent, IconComponent],
  templateUrl: './employee-form.page.html',
  styleUrl: './employee-form.page.scss',
})
export class EmployeeFormPage implements OnInit {
  /** Route parameter (bound by withComponentInputBinding); absent on /employees/new. */
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(EmployeesApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly referenceData = inject(ReferenceDataApiService);

  protected readonly departments = toSignal(this.referenceData.departments(), { initialValue: [] });
  protected readonly taxSchemes = toSignal(this.referenceData.taxSchemes(), { initialValue: [] });
  protected readonly payComponentTypes = toSignal(this.referenceData.payComponentTypes(), { initialValue: [] });

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  private rowVersion = '';

  protected readonly form = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    jobTitle: ['', [Validators.required, Validators.maxLength(100)]],
    departmentId: [0, [Validators.min(1)]],
    taxSchemeId: [1, [Validators.min(1)]],
    hireDate: ['', [Validators.required, notInFuture()]],
    baseMonthlySalary: [0, [Validators.required, Validators.min(0.01), maxTwoDecimals()]],
    status: ['Active' as EmployeeStatus, [Validators.required]],
    payComponents: this.fb.array<PayComponentForm>([], { validators: noDuplicateType() }),
  });

  protected get isEdit(): boolean {
    return !!this.id();
  }

  ngOnInit(): void {
    const id = Number(this.id());
    if (!id) {
      return;
    }

    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (employee) => {
        this.fill(employee);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.formError.set(error.detail ?? error.title);
        this.loading.set(false);
      },
    });
  }

  protected save(): void {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: EmployeeRequest = { ...value, payComponents: value.payComponents };
    const id = Number(this.id());
    const save$: Observable<Employee> = id
      ? this.api.update(id, { ...request, rowVersion: this.rowVersion })
      : this.api.create(request);

    this.saving.set(true);
    save$.subscribe({
      next: (employee) => {
        this.saving.set(false);
        this.notifications.success($localize`:@@employees.saved:${employee.fullName}:name: was saved.`);
        void this.router.navigate(['/employees']);
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

  private fill(employee: Employee): void {
    this.rowVersion = employee.rowVersion;
    this.form.patchValue({
      fullName: employee.fullName,
      email: employee.email,
      jobTitle: employee.jobTitle,
      departmentId: employee.departmentId,
      taxSchemeId: employee.taxSchemeId,
      hireDate: employee.hireDate,
      baseMonthlySalary: employee.baseMonthlySalary,
      status: employee.status,
    });

    const rows = this.form.controls.payComponents;
    rows.clear();
    for (const component of employee.payComponents) {
      rows.push(PayComponentsEditorComponent.createRow(this.fb, component.payComponentTypeId, component.amount));
    }
  }
}
