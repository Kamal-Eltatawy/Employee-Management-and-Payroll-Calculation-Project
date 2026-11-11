import { Routes } from '@angular/router';

export const PAYROLL_POLICY_ROUTES: Routes = [
  {
    path: '',
    title: $localize`:@@title.payrollPolicies:Payroll policies · Payroll`,
    loadComponent: () => import('./pages/payroll-policy-list.page').then((m) => m.PayrollPolicyListPage),
  },
  {
    path: 'new',
    title: $localize`:@@title.newPayrollPolicy:New payroll policy · Payroll`,
    loadComponent: () => import('./pages/payroll-policy-form.page').then((m) => m.PayrollPolicyFormPage),
  },
  {
    path: ':id',
    title: $localize`:@@title.payrollPolicy:Payroll policy · Payroll`,
    loadComponent: () => import('./pages/payroll-policy-form.page').then((m) => m.PayrollPolicyFormPage),
  },
];
