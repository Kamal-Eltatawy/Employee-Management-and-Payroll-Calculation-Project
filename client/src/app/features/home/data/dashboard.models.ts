export interface CountItem {
  name: string;
  count: number;
}

export interface PayrollTotals {
  employeesPaid: number;
  grossPay: number;
  incomeTax: number;
  socialInsurance: number;
  otherDeductions: number;
  netPay: number;
  negativeNetCount: number;
  withoutPolicy: number;
}

export interface Dashboard {
  month: string;
  totalEmployees: number;
  activeEmployees: number;
  inactiveEmployees: number;
  hiredThisMonth: number;
  byDepartment: CountItem[];
  byTaxScheme: CountItem[];
  payroll: PayrollTotals;
}
