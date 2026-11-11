import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { maxTwoDecimals, noDuplicateType, notInFuture, requiredSelection } from './validators';

describe('notInFuture', () => {
  const today = () => new Date(2026, 2, 15); // 15 March 2026, local time
  const validate = (value: string) => notInFuture(today)(new FormControl(value));

  it('accepts today and the past', () => {
    expect(validate('2026-03-15')).toBeNull();
    expect(validate('2024-06-01')).toBeNull();
  });

  it('rejects tomorrow', () => {
    expect(validate('2026-03-16')).toEqual({ notInFuture: true });
  });

  it('leaves empty values to the required validator', () => {
    expect(validate('')).toBeNull();
  });
});

describe('maxTwoDecimals', () => {
  const validate = (value: number) => maxTwoDecimals()(new FormControl(value));

  it('accepts up to two decimals', () => {
    expect(validate(17000)).toBeNull();
    expect(validate(99.5)).toBeNull();
    expect(validate(1.01)).toBeNull();
  });

  it('rejects three decimals', () => {
    expect(validate(100.123)).toEqual({ maxTwoDecimals: true });
  });
});

describe('noDuplicateType', () => {
  const row = (typeId: number) => new FormGroup({ payComponentTypeId: new FormControl(typeId), amount: new FormControl(10) });

  it('accepts distinct types', () => {
    expect(noDuplicateType()(new FormArray([row(1), row(2)]))).toBeNull();
  });

  it('reports the indexes of repeated types', () => {
    expect(noDuplicateType()(new FormArray([row(1), row(2), row(1), row(1)]))).toEqual({ duplicateType: [2, 3] });
  });

  it('ignores rows without a type yet', () => {
    expect(noDuplicateType()(new FormArray([row(0), row(0)]))).toBeNull();
  });
});

describe('requiredSelection', () => {
  const validate = (value: unknown) => requiredSelection()(new FormControl(value));

  it('rejects the placeholder value', () => {
    expect(validate(0)).toEqual({ requiredSelection: true });
    expect(validate(null)).toEqual({ requiredSelection: true });
    expect(validate('')).toEqual({ requiredSelection: true });
  });

  it('accepts a chosen id', () => {
    expect(validate(3)).toBeNull();
  });
});
