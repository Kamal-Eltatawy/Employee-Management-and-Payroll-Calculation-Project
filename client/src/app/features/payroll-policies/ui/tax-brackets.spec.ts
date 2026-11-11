import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { TaxMethod } from '../data/payroll-policy.models';
import { bracketProblems, describeBands, taxBracketsValidator, toTaxBrackets } from './tax-brackets';

const open = (rate: number) => ({ upperBound: null, ratePercent: rate });
const upTo = (upper: number, rate: number) => ({ upperBound: upper, ratePercent: rate });

describe('bracketProblems', () => {
  describe('Progressive', () => {
    it('accepts ascending bounds with only the last row open', () => {
      expect(bracketProblems([upTo(5000, 0), upTo(15000, 10), open(20)], 'Progressive')).toEqual({});
      expect(bracketProblems([open(10)], 'Progressive')).toEqual({});
    });

    it('needs at least one bracket', () => {
      expect(bracketProblems([], 'Progressive')).toEqual({ shape: 'Progressive' });
    });

    it('reports bounds that are not strictly ascending', () => {
      expect(bracketProblems([upTo(5000, 0), upTo(5000, 10), open(20)], 'Progressive')).toEqual({ bounds: { 1: 'ascending' } });
      expect(bracketProblems([upTo(9000, 0), upTo(5000, 10), open(20)], 'Progressive')).toEqual({ bounds: { 1: 'ascending' } });
    });

    it('reports an open row that is not the last, and a closed last row', () => {
      expect(bracketProblems([open(0), upTo(5000, 10)], 'Progressive')).toEqual({ bounds: { 0: 'onlyLastOpen', 1: 'lastOpen' } });
    });
  });

  describe('Flat', () => {
    it('accepts exactly one open row', () => {
      expect(bracketProblems([open(15)], 'Flat')).toEqual({});
    });

    it('rejects no rows or several rows', () => {
      expect(bracketProblems([], 'Flat')).toEqual({ shape: 'Flat' });
      expect(bracketProblems([upTo(5000, 0), open(15)], 'Flat').shape).toBe('Flat');
    });

    it('rejects an upper bound on the only row', () => {
      expect(bracketProblems([upTo(5000, 15)], 'Flat')).toEqual({ bounds: { 0: 'lastOpen' } });
    });
  });

  describe('Exempt', () => {
    it('accepts no rows and rejects any row', () => {
      expect(bracketProblems([], 'Exempt')).toEqual({});
      expect(bracketProblems([open(0)], 'Exempt').shape).toBe('Exempt');
    });
  });
});

describe('taxBracketsValidator', () => {
  const row = (upper: number | null, rate: number) =>
    new FormGroup({ upperBound: new FormControl<number | null>(upper), ratePercent: new FormControl<number | null>(rate) });

  it('reads the current method on every run', () => {
    let method: TaxMethod = 'Progressive';
    const array = new FormArray([row(5000, 0), row(null, 10)], { validators: taxBracketsValidator(() => method) });
    expect(array.errors).toBeNull();

    method = 'Flat';
    array.updateValueAndValidity();
    expect(array.errors).toEqual({ bracketShape: 'Flat' });

    method = 'Exempt';
    array.updateValueAndValidity();
    expect(array.errors).toEqual({ bracketShape: 'Exempt' });
  });

  it('reports row problems by index', () => {
    const array = new FormArray([row(5000, 0), row(4000, 10), row(null, 20)], { validators: taxBracketsValidator(() => 'Progressive') });

    expect(array.errors).toEqual({ bracketBounds: { 1: 'ascending' } });
  });
});

describe('describeBands', () => {
  it('describes closed bands, the open top band and a single open band', () => {
    expect(describeBands([upTo(5000, 0), upTo(15000, 10), open(20)], 'en')).toEqual([
      '0.00 – 5,000.00: 0%',
      '5,000.00 – 15,000.00: 10%',
      'Above 15,000.00: 20%',
    ]);
    expect(describeBands([open(12.5)], 'en')).toEqual(['All taxable income: 12.5%']);
  });

  it('skips rows without a rate', () => {
    expect(describeBands([{ upperBound: 5000, ratePercent: null }, open(20)], 'en')).toEqual(['Above 5,000.00: 20%']);
  });
});

describe('toTaxBrackets', () => {
  it('sends empty upper bounds as null', () => {
    expect(toTaxBrackets([upTo(5000, 0), { upperBound: null, ratePercent: 20 }])).toEqual([upTo(5000, 0), open(20)]);
  });
});
