import { Routes } from '@angular/router';

export const EMPLOYEE_ROUTES: Routes = [
  {
    path: '',
    title: $localize`:@@title.employees:Employees · Payroll`,
    loadComponent: () => import('./pages/employee-list.page').then((m) => m.EmployeeListPage),
  },
  {
    path: 'new',
    title: $localize`:@@title.newEmployee:New employee · Payroll`,
    loadComponent: () => import('./pages/employee-form.page').then((m) => m.EmployeeFormPage),
  },
  {
    path: ':id/edit',
    title: $localize`:@@title.editEmployee:Edit employee · Payroll`,
    loadComponent: () => import('./pages/employee-form.page').then((m) => m.EmployeeFormPage),
  },
  {
    path: ':id/payslip',
    title: $localize`:@@title.payslip:Payslip · Payroll`,
    loadComponent: () => import('../payslip/pages/payslip.page').then((m) => m.PayslipPage),
  },
];
