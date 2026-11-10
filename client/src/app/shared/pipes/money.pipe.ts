import { Pipe, PipeTransform } from '@angular/core';

const formatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  // Intl's default rounding is "halfExpand": half away from zero (D33).
  roundingMode: 'halfExpand',
} as Intl.NumberFormatOptions);

/** The only place where money is rounded: display, 2 decimals, half away from zero (D32/D33). */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value === null || value === undefined || Number.isNaN(value) ? '' : formatter.format(value);
  }
}
