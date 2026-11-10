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
  readonly label = input('This field');

  protected messages(): string[] {
    const errors = this.control().errors ?? {};
    const label = this.label();
    return Object.entries(errors).map(([key, value]) => {
      switch (key) {
        case 'required':
          return `${label} is required.`;
        case 'maxlength':
          return `${label} must be at most ${(value as { requiredLength: number }).requiredLength} characters.`;
        case 'email':
          return 'Enter a valid email address.';
        case 'min':
          return `${label} must be greater than zero.`;
        case 'notInFuture':
          return `${label} cannot be in the future.`;
        case 'maxTwoDecimals':
          return `${label} can have at most two decimal places.`;
        case 'server':
          return String(value);
        default:
          return `${label} is not valid.`;
      }
    });
  }
}
