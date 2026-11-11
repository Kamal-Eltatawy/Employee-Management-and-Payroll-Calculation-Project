import { Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { startWith, switchMap } from 'rxjs';

/** Renders client-side and server-side messages for one control in the same way (D42). */
@Component({
  selector: 'app-field-errors',
  template: `
    @for (message of messages(); track message) {
      <div class="invalid-feedback d-block">{{ message }}</div>
    }
  `,
})
export class FieldErrorsComponent {
  readonly control = input.required<AbstractControl>();
  readonly label = input($localize`:@@field.default:This field`);
  /** Shown when nothing is selected in a dropdown (requiredSelection), e.g. "Please select a department." */
  readonly selectMessage = input<string>();

  /**
   * Components are OnPush by default (Angular 22) and form state is not a signal: follow the control's events
   * (touched on submit, status, server errors) so the messages appear without an event inside this component.
   */
  private readonly controlEvent = toSignal(
    toObservable(this.control).pipe(switchMap((control) => control.events.pipe(startWith(null)))),
  );

  protected readonly messages = computed(() => {
    this.controlEvent();
    const control = this.control();
    return control.invalid && (control.touched || control.dirty) ? this.describe(control) : [];
  });

  private describe(control: AbstractControl): string[] {
    const errors = control.errors ?? {};
    const label = this.label();
    return Object.entries(errors).map(([key, value]) => {
      switch (key) {
        case 'required':
          return $localize`:@@validation.required:${label}:label: is required.`;
        case 'maxlength': {
          const max = (value as { requiredLength: number }).requiredLength;
          return $localize`:@@validation.maxLength:${label}:label: must be at most ${max}:max: characters.`;
        }
        case 'email':
          return $localize`:@@validation.email:Enter a valid email address.`;
        case 'requiredSelection':
          return this.selectMessage() ?? $localize`:@@validation.select:Please select ${label}:label:.`;
        case 'min':
          return $localize`:@@validation.positive:${label}:label: must be greater than zero.`;
        case 'notInFuture':
          return $localize`:@@validation.notInFuture:${label}:label: cannot be in the future.`;
        case 'maxTwoDecimals':
          return $localize`:@@validation.twoDecimals:${label}:label: can have at most two decimal places.`;
        case 'server':
          return String(value);
        default:
          return $localize`:@@validation.invalid:${label}:label: is not valid.`;
      }
    });
  }
}
