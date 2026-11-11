import { TestBed } from '@angular/core/testing';
import { FormArray, FormGroup, NonNullableFormBuilder } from '@angular/forms';
import { applyServerErrors } from '../../../shared/forms/apply-server-errors';
import { TaxMethod } from '../data/payroll-policy.models';
import { TaxBracketForm, createBracketRow, taxBracketsValidator } from './tax-brackets';
import { TaxBracketsEditorComponent } from './tax-brackets-editor.component';

describe('TaxBracketsEditorComponent', () => {
  async function render(method: TaxMethod, rows: [number | null, number][]) {
    const fb = TestBed.inject(NonNullableFormBuilder);
    const array = new FormArray<TaxBracketForm>(
      rows.map(([upper, rate]) => createBracketRow(fb, upper, rate)),
      { validators: taxBracketsValidator(() => method) },
    );
    const form = new FormGroup({ taxBrackets: array });
    const fixture = TestBed.createComponent(TaxBracketsEditorComponent);
    fixture.componentRef.setInput('array', array);
    fixture.componentRef.setInput('method', method);
    await fixture.whenStable();
    return { fixture, array, form, element: fixture.nativeElement as HTMLElement };
  }

  it('adds and removes progressive rows', async () => {
    const { fixture, array, element } = await render('Progressive', [[5000, 0], [null, 10]]);

    (element.querySelector('.editor-head button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(array.length).toBe(3);
    expect(element.querySelectorAll('.item').length).toBe(3);

    (element.querySelectorAll('.item .remove')[2] as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(array.length).toBe(2);
    expect(element.querySelectorAll('.item').length).toBe(2);
  });

  it('shows a live preview of the bands', async () => {
    const { element } = await render('Progressive', [[5000, 0], [15000, 10], [null, 20]]);

    const bands = Array.from(element.querySelectorAll('.preview li')).map((item) => item.textContent?.trim());
    expect(bands).toEqual(['0.00 – 5,000.00: 0%', '5,000.00 – 15,000.00: 10%', 'Above 15,000.00: 20%']);
  });

  it('shows the row problem once the brackets were touched', async () => {
    const { fixture, array, element } = await render('Progressive', [[5000, 0], [4000, 10], [null, 20]]);
    expect(element.textContent).not.toContain('greater than the previous upper bound');

    array.markAllAsTouched();
    await fixture.whenStable();

    const items = element.querySelectorAll('.item');
    expect(items[1].textContent).toContain('Must be greater than the previous upper bound.');
    expect(items[1].querySelector('input')?.classList).toContain('is-invalid');
  });

  it('puts server errors on the right row and on the list', async () => {
    const { fixture, form, element } = await render('Progressive', [[5000, 0], [15000, 10], [null, 20]]);

    applyServerErrors(form, {
      status: 400,
      title: 'Invalid.',
      fieldErrors: { 'taxBrackets[1].ratePercent': ['Rate is too high.'], taxBrackets: ['A progressive tax needs at least one tax bracket.'] },
    });
    await fixture.whenStable();

    expect(element.querySelectorAll('.item')[1].textContent).toContain('Rate is too high.');
    expect(element.querySelectorAll('.item')[0].textContent).not.toContain('Rate is too high.');
    expect(element.textContent).toContain('A progressive tax needs at least one tax bracket.');
  });

  it('shows one rate field and no add button for a flat tax', async () => {
    const { element } = await render('Flat', [[null, 15]]);

    expect(element.querySelector('.editor-head button')).toBeNull();
    expect(element.querySelectorAll('.item input').length).toBe(1);
    expect(element.textContent).toContain('All taxable income: 15%');
  });

  it('explains instead of showing an editor for an exempt scheme', async () => {
    const { element } = await render('Exempt', []);

    expect(element.querySelector('input')).toBeNull();
    expect(element.textContent).toContain('tax exempt');
  });
});
