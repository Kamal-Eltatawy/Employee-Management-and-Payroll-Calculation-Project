import { Component, OnInit, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiError } from '../../../core/http/api-error';
import { NotificationService } from '../../../core/notifications/notification.service';
import { applyServerErrors } from '../../../shared/forms/apply-server-errors';
import { FieldErrorsComponent } from '../../../shared/ui/field-errors.component';
import { IconComponent } from '../../../shared/ui/icon.component';
import { PayComponentTypesApiService } from '../data/pay-component-types-api.service';
import { PayComponentKind, PayComponentType } from '../data/pay-component-type.models';

/** The server's PayComponentType.NameMaxLength. */
const NAME_MAX_LENGTH = 100;

/**
 * Create and edit one pay item. A deduction is never taxable. The kind can change only while no employee uses the item
 * (D71): payslips are not stored but recalculated, so changing a used item's kind would change past payslips.
 */
@Component({
  selector: 'app-pay-component-type-form-page',
  imports: [ReactiveFormsModule, RouterLink, FieldErrorsComponent, IconComponent],
  templateUrl: './pay-component-type-form.page.html',
  styleUrl: '../../employees/pages/employee-form.page.scss',
})
export class PayComponentTypeFormPage implements OnInit {
  /** Route parameter (bound by withComponentInputBinding); absent on /pay-items/new. */
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(PayComponentTypesApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  /** Edit only: the item is assigned to employees, so the kind is locked (D71). */
  protected readonly isInUse = signal(false);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)]],
    nameAr: ['', [Validators.maxLength(NAME_MAX_LENGTH)]],
    kind: ['Allowance' as PayComponentKind, [Validators.required]],
    isTaxable: [true],
  });

  constructor() {
    this.form.controls.kind.valueChanges.pipe(takeUntilDestroyed()).subscribe((kind) => this.applyKind(kind));
  }

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
      next: (type) => {
        this.fill(type);
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
    const name = value.name.trim();
    const nameAr = value.nameAr.trim() || null;
    const isTaxable = value.kind === 'Deduction' ? false : value.isTaxable;
    const id = Number(this.id());
    const save$: Observable<PayComponentType> = id
      ? this.api.update(id, { name, nameAr, isTaxable, kind: value.kind })
      : this.api.create({ name, nameAr, isTaxable, kind: value.kind });

    this.saving.set(true);
    save$.subscribe({
      next: (type) => {
        this.saving.set(false);
        this.notifications.success($localize`:@@payItems.saved:${type.name}:name: was saved.`);
        void this.router.navigate(['/pay-items']);
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

  /** A deduction is never taxable: the checkbox is cleared and locked (the server rejects it with 400). */
  private applyKind(kind: PayComponentKind): void {
    const isTaxable = this.form.controls.isTaxable;
    if (kind === 'Deduction') {
      isTaxable.setValue(false);
      isTaxable.disable();
    } else {
      isTaxable.enable();
    }
  }

  private fill(type: PayComponentType): void {
    this.form.patchValue({ name: type.nameEn, nameAr: type.nameAr ?? '', kind: type.kind, isTaxable: type.isTaxable });
    this.applyKind(type.kind);
    this.isInUse.set(!!type.isInUse);
    if (type.isInUse) {
      this.form.controls.kind.disable({ emitEvent: false });
    }
  }
}
