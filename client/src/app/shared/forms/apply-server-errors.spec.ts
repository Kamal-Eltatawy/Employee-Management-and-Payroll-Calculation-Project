import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { applyServerErrors } from './apply-server-errors';

describe('applyServerErrors', () => {
  const createForm = () =>
    new FormGroup({
      email: new FormControl('ali@demo.local'),
      payComponents: new FormArray([
        new FormGroup({ payComponentTypeId: new FormControl(1), amount: new FormControl(10) }),
      ]),
    });

  it('puts a server error on a top-level control and marks it touched', () => {
    const form = createForm();

    const applied = applyServerErrors(form, { status: 409, title: 'Conflict.', fieldErrors: { email: ['Email is already in use.'] } });

    expect(applied).toBe(true);
    expect(form.controls.email.errors).toEqual({ server: 'Email is already in use.' });
    expect(form.controls.email.touched).toBe(true);
  });

  it('follows JSON paths into form arrays', () => {
    const form = createForm();

    applyServerErrors(form, {
      status: 400,
      title: 'Invalid.',
      fieldErrors: { 'payComponents[0].payComponentTypeId': ['Pay component type does not exist.'] },
    });

    expect(form.controls.payComponents.at(0).controls.payComponentTypeId.errors).toEqual({
      server: 'Pay component type does not exist.',
    });
  });

  it('returns false when no key matches a control', () => {
    const form = createForm();

    expect(applyServerErrors(form, { status: 400, title: 'Invalid.', fieldErrors: { rowVersion: ['Bad.'] } })).toBe(false);
    expect(applyServerErrors(form, { status: 409, title: 'Conflict.', fieldErrors: {} })).toBe(false);
  });
});
