import { TestBed } from '@angular/core/testing';
import { FormArray, NonNullableFormBuilder } from '@angular/forms';
import { noDuplicateType } from '../../../shared/forms/validators';
import { PayComponentType } from '../data/employee.models';
import { PayComponentForm } from './pay-component-form';
import { PayComponentsEditorComponent } from './pay-components-editor.component';

describe('PayComponentsEditorComponent', () => {
  const types: PayComponentType[] = [
    { id: 1, name: 'Housing', kind: 'Allowance', isTaxable: true, nameEn: 'Housing', nameAr: null },
    { id: 2, name: 'Loan', kind: 'Deduction', isTaxable: false, nameEn: 'Loan', nameAr: null },
  ];

  async function render(rows: [number, number][]) {
    const fb = TestBed.inject(NonNullableFormBuilder);
    const array = new FormArray<PayComponentForm>(
      rows.map(([typeId, amount]) => PayComponentsEditorComponent.createRow(fb, typeId, amount)),
      { validators: noDuplicateType() },
    );
    const fixture = TestBed.createComponent(PayComponentsEditorComponent);
    fixture.componentRef.setInput('array', array);
    fixture.componentRef.setInput('types', types);
    await fixture.whenStable();
    return { fixture, array, element: fixture.nativeElement as HTMLElement };
  }

  it('marks only the repeated row as a duplicate', async () => {
    const { element } = await render([[1, 100], [2, 50], [1, 200]]);

    const selects = Array.from(element.querySelectorAll('select'));
    expect(selects.map((select) => select.classList.contains('is-invalid'))).toEqual([false, false, true]);
    expect(element.querySelectorAll('.item')[2].textContent).toContain('This type is already in the list.');
    expect(element.textContent?.match(/already in the list/g)?.length).toBe(1);
  });

  it('asks for a type when none is selected', async () => {
    const { fixture, array, element } = await render([[0, 100]]);
    array.markAllAsTouched(); // as the page's submit does: no event inside the editor
    await fixture.whenStable();

    expect(element.textContent).toContain('Please select a type.');
    expect(element.textContent).not.toContain('greater than zero');
  });

  it('adds and removes rows', async () => {
    const { fixture, array, element } = await render([]);
    expect(element.textContent).toContain('No allowances or deductions.');

    (element.querySelector('.editor-head button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(array.length).toBe(1);

    (element.querySelector('.item .icon-button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(array.length).toBe(0);
  });
});
