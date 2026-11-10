import { Routes } from '@angular/router';

export const EMPLOYEE_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./pages/employee-list.page').then((m) => m.EmployeeListPage) },
  { path: 'new', loadComponent: () => import('./pages/employee-form.page').then((m) => m.EmployeeFormPage) },
  { path: ':id/edit', loadComponent: () => import('./pages/employee-form.page').then((m) => m.EmployeeFormPage) },
  { path: ':id/payslip', loadComponent: () => import('../payslip/pages/payslip.page').then((m) => m.PayslipPage) },
];
