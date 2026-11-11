import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';
import { AuthService } from './core/auth/auth.service';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: () => inject(AuthService).homeUrl() },
  {
    path: 'login',
    title: $localize`:@@title.login:Sign in · Payroll`,
    canActivate: [guestGuard],
    loadComponent: () => import('./core/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'home',
    title: $localize`:@@title.home:Home · Payroll`,
    canActivate: [authGuard],
    loadComponent: () => import('./features/home/pages/home.page').then((m) => m.HomePage),
  },
  {
    path: 'employees',
    canActivate: [authGuard, roleGuard('Hr')],
    loadChildren: () => import('./features/employees/employees.routes').then((m) => m.EMPLOYEE_ROUTES),
  },
  {
    path: 'pay-items',
    canActivate: [authGuard, roleGuard('Hr')],
    loadChildren: () => import('./features/pay-component-types/pay-component-types.routes').then((m) => m.PAY_COMPONENT_TYPE_ROUTES),
  },
  {
    path: 'payroll-policies',
    canActivate: [authGuard, roleGuard('Hr')],
    loadChildren: () => import('./features/payroll-policies/payroll-policies.routes').then((m) => m.PAYROLL_POLICY_ROUTES),
  },
  {
    path: 'my-payslip',
    title: $localize`:@@title.myPayslip:My payslip · Payroll`,
    canActivate: [authGuard, roleGuard('Employee')],
    loadComponent: () => import('./features/payslip/pages/payslip.page').then((m) => m.PayslipPage),
  },
  { path: '**', redirectTo: '' },
];
