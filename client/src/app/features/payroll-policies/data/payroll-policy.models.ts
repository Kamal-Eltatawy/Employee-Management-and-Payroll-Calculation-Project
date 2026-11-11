import { TaxMethod, TaxScheme } from '../../employees/data/employee.models';

export type { TaxMethod, TaxScheme };

/** A null upper bound is the open-ended top bracket. */
export interface TaxBracket {
  upperBound: number | null;
  ratePercent: number;
}

export interface PayrollPolicy {
  id: number;
  taxSchemeId: number;
  /** yyyy-MM-dd, always the first day of a month. */
  effectiveFrom: string;
  insuranceRatePercent: number;
  /** Null = no cap; 0 is a real cap. */
  insuranceCap: number | null;
  taxBrackets: TaxBracket[];
  /** Only policies starting in the current month or later can be changed or deleted (D54). */
  isModifiable: boolean;
}

export interface PayrollPolicyRequest {
  taxSchemeId: number;
  effectiveFrom: string;
  insuranceRatePercent: number;
  insuranceCap: number | null;
  taxBrackets: TaxBracket[];
}
