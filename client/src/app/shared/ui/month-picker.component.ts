import { Component, LOCALE_ID, computed, inject, input, output } from '@angular/core';
import { monthNames } from '../util/months';

/**
 * Month + year selects in the UI language. Replaces <input type="month">, whose text follows
 * the browser's language instead of the app's (English inside the Arabic UI).
 */
@Component({
  selector: 'app-month-picker',
  template: `
    <div class="picker" role="group" aria-label="Month" i18n-aria-label="@@payslip.month">
      <select class="form-select" [value]="month()" (change)="emit(year(), +monthSelect.value)" #monthSelect
              aria-label="Month" i18n-aria-label="@@payslip.month">
        @for (name of names; track $index) {
          <option [value]="$index + 1" [selected]="$index + 1 === month()">{{ name }}</option>
        }
      </select>
      <select class="form-select year" (change)="emit(+yearSelect.value, month())" #yearSelect
              aria-label="Year" i18n-aria-label="@@monthPicker.year">
        @for (y of years(); track y) {
          <option [value]="y" [selected]="y === year()">{{ y }}</option>
        }
      </select>
    </div>
  `,
  styles: `
    .picker { display: flex; gap: .5rem; }
    .picker select { min-width: 9rem; }
    .picker .year { min-width: 6.5rem; }
  `,
})
export class MonthPickerComponent {
  /** yyyy-MM */
  readonly value = input.required<string>();
  readonly valueChange = output<string>();

  protected readonly names = monthNames(inject(LOCALE_ID));
  protected readonly year = computed(() => Number(this.value().slice(0, 4)));
  protected readonly month = computed(() => Number(this.value().slice(5, 7)));
  /** Five years back to two ahead (future months are previews, D34). */
  protected readonly years = computed(() => {
    const current = new Date().getFullYear();
    const range = Array.from({ length: 8 }, (_, i) => current - 5 + i);
    return range.includes(this.year()) ? range : [...range, this.year()].sort();
  });

  protected emit(year: number, month: number): void {
    this.valueChange.emit(`${year}-${String(month).padStart(2, '0')}`);
  }
}
