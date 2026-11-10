import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { Department, PayComponentType, TaxScheme } from './employee.models';

/** Master data used by filters and forms. Cached for the session: it changes rarely. */
@Injectable({ providedIn: 'root' })
export class ReferenceDataApiService {
  private readonly http = inject(HttpClient);

  private readonly departments$ = this.http.get<Department[]>('/api/departments').pipe(shareReplay(1));
  private readonly payComponentTypes$ = this.http.get<PayComponentType[]>('/api/pay-component-types').pipe(shareReplay(1));
  private readonly taxSchemes$ = this.http.get<TaxScheme[]>('/api/tax-schemes').pipe(shareReplay(1));

  departments(): Observable<Department[]> {
    return this.departments$;
  }

  payComponentTypes(): Observable<PayComponentType[]> {
    return this.payComponentTypes$;
  }

  taxSchemes(): Observable<TaxScheme[]> {
    return this.taxSchemes$;
  }
}
