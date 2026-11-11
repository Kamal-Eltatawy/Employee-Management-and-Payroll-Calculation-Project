import { AbstractControl, FormArray, FormControl, FormGroup, NonNullableFormBuilder, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { formatMoney } from '../../../shared/pipes/money.pipe';
import { maxTwoDecimals, percentRange } from '../../../shared/forms/validators';
import { TaxBracket, TaxMethod } from '../data/payroll-policy.models';

export type TaxBracketForm = FormGroup<{
  upperBound: FormControl<number | null>;
  ratePercent: FormControl<number | null>;
}>;

/** Why one bracket's upper bound is wrong; the same order of checks as the server's PayrollPolicyRequestValidator. */
export type BracketBoundProblem = 'lastOpen' | 'onlyLastOpen' | 'ascending';

export interface BracketProblems {
  /** The number of brackets does not fit the scheme's tax method (TaxScheme.DescribeBracketShapeProblem). */
  shape?: TaxMethod;
  /** Per row index. */
  bounds?: Record<number, BracketBoundProblem>;
}

interface BracketValue {
  upperBound: number | null;
  ratePercent?: number | null;
}

export function createBracketRow(fb: NonNullableFormBuilder, upperBound: number | null = null, ratePercent: number | null = null): TaxBracketForm {
  return fb.group({
    upperBound: fb.control<number | null>(upperBound, [Validators.min(0.01), maxTwoDecimals()]),
    ratePercent: fb.control<number | null>(ratePercent, [Validators.required, percentRange(), maxTwoDecimals()]),
  });
}

/**
 * Mirrors the server rules: Progressive needs at least one bracket, Flat exactly one, Exempt none (shape);
 * only the last bracket is open-ended and upper bounds are strictly ascending (bounds).
 */
export function bracketProblems(brackets: readonly BracketValue[], method: TaxMethod | undefined): BracketProblems {
  const problems: BracketProblems = {};
  const count = brackets.length;
  if ((method === 'Progressive' && count === 0) || (method === 'Flat' && count !== 1) || (method === 'Exempt' && count !== 0)) {
    problems.shape = method;
  }

  const bounds: Record<number, BracketBoundProblem> = {};
  brackets.forEach((bracket, index) => {
    const upper = numberOrNull(bracket.upperBound);
    const previous = index > 0 ? numberOrNull(brackets[index - 1].upperBound) : null;
    const isLast = index === count - 1;
    if (isLast && upper !== null) {
      bounds[index] = 'lastOpen';
    } else if (!isLast && upper === null) {
      bounds[index] = 'onlyLastOpen';
    } else if (upper !== null && previous !== null && upper <= previous) {
      bounds[index] = 'ascending';
    }
  });
  if (Object.keys(bounds).length > 0) {
    problems.bounds = bounds;
  }

  return problems;
}

/** On the brackets FormArray. The method can change with the selected scheme, so it is read on every run. */
export function taxBracketsValidator(method: () => TaxMethod | undefined): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (!(control instanceof FormArray)) {
      return null;
    }

    const problems = bracketProblems(control.getRawValue() as BracketValue[], method());
    const errors: ValidationErrors = {};
    if (problems.shape) {
      errors['bracketShape'] = problems.shape;
    }
    if (problems.bounds) {
      errors['bracketBounds'] = problems.bounds;
    }

    return Object.keys(errors).length > 0 ? errors : null;
  };
}

/** A rate as "12.5%" with Latin digits in every language (like money). */
export function formatPercent(value: number, locale: string): string {
  const number = new Intl.NumberFormat(locale, { maximumFractionDigits: 2, numberingSystem: 'latn' } as Intl.NumberFormatOptions).format(value);
  return `${number}%`;
}

/** Human-readable bands, e.g. "0.00 – 5,000.00: 0%" and "Above 15,000.00: 20%". Rows without a valid rate are skipped. */
export function describeBands(brackets: readonly BracketValue[], locale: string): string[] {
  const bands: string[] = [];
  brackets.forEach((bracket, index) => {
    const rateValue = numberOrNull(bracket.ratePercent ?? null);
    if (rateValue === null) {
      return;
    }

    const rate = formatPercent(rateValue, locale);
    const lower = formatMoney(index > 0 ? (numberOrNull(brackets[index - 1].upperBound) ?? 0) : 0, locale);
    const upperValue = numberOrNull(bracket.upperBound);
    if (upperValue !== null) {
      const upper = formatMoney(upperValue, locale);
      bands.push($localize`:@@brackets.bandRange:${lower}:lower: – ${upper}:upper:: ${rate}:rate:`);
    } else if (index === 0) {
      bands.push($localize`:@@brackets.bandAll:All taxable income: ${rate}:rate:`);
    } else {
      bands.push($localize`:@@brackets.bandAbove:Above ${lower}:lower:: ${rate}:rate:`);
    }
  });

  return bands;
}

export function taxMethodLabel(method: TaxMethod | undefined): string {
  switch (method) {
    case 'Progressive':
      return $localize`:@@taxMethod.progressive:Progressive brackets`;
    case 'Flat':
      return $localize`:@@taxMethod.flat:Flat rate`;
    case 'Exempt':
      return $localize`:@@taxMethod.exempt:Tax exempt`;
    default:
      return '';
  }
}

/** The request shape: empty inputs become null upper bounds. */
export function toTaxBrackets(rows: readonly BracketValue[]): TaxBracket[] {
  return rows.map((row) => ({ upperBound: numberOrNull(row.upperBound), ratePercent: Number(row.ratePercent) }));
}

function numberOrNull(value: number | null | undefined): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}
