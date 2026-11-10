import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

const formatters = new Map<string, Intl.NumberFormat>();

/**
 * The only place where money is rounded: display, 2 decimals, half away from zero (D32/D33).
 * Separators follow the language; digits stay Latin (0-9) in every language for unambiguous amounts.
 */
export function formatMoney(value: number | null | undefined, locale = 'en'): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '';
  }

  let formatter = formatters.get(locale);
  if (!formatter) {
    formatter = new Intl.NumberFormat(locale, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
      numberingSystem: 'latn',
      roundingMode: 'halfExpand',
    } as Intl.NumberFormatOptions);
    formatters.set(locale, formatter);
  }

  return formatter.format(value);
}

@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(value: number | null | undefined): string {
    return formatMoney(value, this.locale);
  }
}
