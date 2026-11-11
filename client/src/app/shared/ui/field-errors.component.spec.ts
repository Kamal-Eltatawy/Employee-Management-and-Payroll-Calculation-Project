import { TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { requiredSelection } from '../forms/validators';
import { FieldErrorsComponent } from './field-errors.component';

describe('FieldErrorsComponent', () => {
  async function render(control: FormControl, inputs: Record<string, string> = {}) {
    const fixture = TestBed.createComponent(FieldErrorsComponent);
    fixture.componentRef.setInput('control', control);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    return Array.from(element.querySelectorAll('.invalid-feedback')).map((node) => node.textContent?.trim());
  }

  it('shows nothing until the control is touched', async () => {
    expect(await render(new FormControl('', Validators.required), { label: 'Email' })).toEqual([]);
  });

  it('uses the label in the message', async () => {
    const control = new FormControl('', Validators.required);
    control.markAsTouched();

    expect(await render(control, { label: 'Email' })).toEqual(['Email is required.']);
  });

  it('shows the dropdown message when nothing is selected, not the minimum message', async () => {
    const control = new FormControl(0, requiredSelection());
    control.markAsTouched();

    expect(await render(control, { label: 'Department', selectMessage: 'Please select a department.' })).toEqual([
      'Please select a department.',
    ]);
    expect(await render(control, { label: 'Department' })).toEqual(['Please select Department.']);
  });

  it('shows a server message as it is', async () => {
    const control = new FormControl('a@b.c');
    control.setErrors({ server: 'Email is already in use.' });
    control.markAsTouched();

    expect(await render(control)).toEqual(['Email is already in use.']);
  });
});
