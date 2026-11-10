import { TaxMethod } from '../../employees/data/employee.models';

export interface PayslipLine {
  name: string;
  amount: number;
  isTaxable: boolean;
}

/** Every amount is at full precision; render through MoneyPipe (D32/D33). */
export interface Payslip {
  employeeId: number;
  employeeName: string;
  month: string;
  policyId: number;
  policyEffectiveFrom: string;
  taxSchemeName: string;
  taxMethod: TaxMethod;
  prorationFactor: number;
  daysPaid: number;
  daysInMonth: number;
  baseSalary: number;
  allowances: PayslipLine[];
  grossPay: number;
  taxableIncome: number;
  incomeTax: number;
  socialInsurance: number;
  deductions: PayslipLine[];
  otherDeductionsTotal: number;
  netPay: number;
  isNetNegative: boolean;
}
