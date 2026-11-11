import { AbstractControl, FormArray, ValidationErrors, ValidatorFn } from '@angular/forms';
import { toIsoDate } from '../util/dates';

/** The date (yyyy-MM-dd) must not be after today in the browser's local time. Mirrors the server rule. */
export function notInFuture(today: () => Date = () => new Date()): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    if (typeof value !== 'string' || value === '') {
      return null;
    }

    return value > toIsoDate(today()) ? { notInFuture: true } : null;
  };
}

/** A select whose placeholder option has the value 0 (or empty): something must be chosen. */
export function requiredSelection(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    const chosen = typeof value === 'number' ? value > 0 : value !== null && value !== undefined && value !== '';
    return chosen ? null : { requiredSelection: true };
  };
}

/** At most two decimal places (the server's PrecisionScale(18, 2)). */
export function maxTwoDecimals(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    if (typeof value !== 'number' || !Number.isFinite(value)) {
      return null;
    }

    return Math.abs(Math.round(value * 100) - value * 100) > 1e-6 ? { maxTwoDecimals: true } : null;
  };
}

/** On a FormArray of groups: the same type may appear only once. Reports the repeated row indexes. */
export function noDuplicateType(key = 'payComponentTypeId'): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (!(control instanceof FormArray)) {
      return null;
    }

    const seen = new Set<unknown>();
    const duplicates: number[] = [];
    control.controls.forEach((row, index) => {
      const value: unknown = row.get(key)?.value;
      if (value === null || value === undefined || value === 0 || value === '') {
        return;
      }

      if (seen.has(value)) {
        duplicates.push(index);
      }
      seen.add(value);
    });

    return duplicates.length > 0 ? { duplicateType: duplicates } : null;
  };
}
