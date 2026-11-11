import { Routes } from '@angular/router';

export const PAY_COMPONENT_TYPE_ROUTES: Routes = [
  {
    path: '',
    title: $localize`:@@title.payItems:Pay items · Payroll`,
    loadComponent: () => import('./pages/pay-component-type-list.page').then((m) => m.PayComponentTypeListPage),
  },
  {
    path: 'new',
    title: $localize`:@@title.newPayItem:New pay item · Payroll`,
    loadComponent: () => import('./pages/pay-component-type-form.page').then((m) => m.PayComponentTypeFormPage),
  },
  {
    path: ':id/edit',
    title: $localize`:@@title.editPayItem:Edit pay item · Payroll`,
    loadComponent: () => import('./pages/pay-component-type-form.page').then((m) => m.PayComponentTypeFormPage),
  },
];
