import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/auth.guards';
import { AuthService } from './core/auth/auth.service';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: () => inject(AuthService).homeUrl() },
  { path: 'login', loadComponent: () => import('./core/auth/login.page').then((m) => m.LoginPage) },
  {
    path: 'employees',
    canActivate: [authGuard, roleGuard('Hr')],
    loadChildren: () => import('./features/employees/employees.routes').then((m) => m.EMPLOYEE_ROUTES),
  },
  {
    path: 'my-payslip',
    canActivate: [authGuard, roleGuard('Employee')],
    loadComponent: () => import('./features/payslip/pages/payslip.page').then((m) => m.PayslipPage),
  },
  { path: '**', redirectTo: '' },
];
