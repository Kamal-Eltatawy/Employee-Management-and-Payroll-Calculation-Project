import { Component, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';

/** Renders client-side and server-side messages for one control in the same way (D42). */
@Component({
  selector: 'app-field-errors',
  template: `
    @if (control().invalid && (control().touched || control().dirty)) {
      @for (message of messages(); track message) {
        <div class="invalid-feedback d-block">{{ message }}</div>
      }
    }
  `,
})
export class FieldErrorsComponent {
  readonly control = input.required<AbstractControl>();
  readonly label = input($localize`:@@field.default:This field`);

  protected messages(): string[] {
    const errors = this.control().errors ?? {};
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
