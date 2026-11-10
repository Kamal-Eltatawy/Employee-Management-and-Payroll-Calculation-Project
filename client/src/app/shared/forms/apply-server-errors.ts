import { AbstractControl } from '@angular/forms';
import { ApiError } from '../../core/http/api-error';

/**
 * Puts each server field error on the matching control as a `server` error (D42).
 * Keys are JSON paths such as "email" or "payComponents[0].amount".
 * Returns true when at least one error was placed on a control.
 */
export function applyServerErrors(form: AbstractControl, error: ApiError): boolean {
  let applied = false;

  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    const path = key
      .split(/[.[\]]/)
      .filter((segment) => segment !== '')
      .map((segment) => (/^\d+$/.test(segment) ? Number(segment) : segment));

    const control = path.length > 0 ? form.get(path) : null;
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
      control.markAsTouched();
      applied = true;
    }
  }

  return applied;
}
