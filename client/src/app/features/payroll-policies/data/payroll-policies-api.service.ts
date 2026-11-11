import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PayrollPolicy, PayrollPolicyRequest } from './payroll-policy.models';

/** Thin typed wrapper over /api/payroll-policies (all tax schemes). */
@Injectable({ providedIn: 'root' })
export class PayrollPoliciesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/payroll-policies';

  list(): Observable<PayrollPolicy[]> {
    return this.http.get<PayrollPolicy[]>(this.baseUrl);
  }

  get(id: number): Observable<PayrollPolicy> {
    return this.http.get<PayrollPolicy>(`${this.baseUrl}/${id}`);
  }

  create(request: PayrollPolicyRequest): Observable<PayrollPolicy> {
    return this.http.post<PayrollPolicy>(this.baseUrl, request);
  }

  update(id: number, request: PayrollPolicyRequest): Observable<PayrollPolicy> {
    return this.http.put<PayrollPolicy>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
