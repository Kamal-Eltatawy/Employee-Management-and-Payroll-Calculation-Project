import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'employees', loadChildren: () => import('./features/employees/employees.routes').then((m) => m.EMPLOYEE_ROUTES) },
  { path: '**', redirectTo: 'employees' },
];
