import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Payslip } from './payslip.models';

@Injectable({ providedIn: 'root' })
export class PayslipApiService {
  private readonly http = inject(HttpClient);

  get(employeeId: number, month: string): Observable<Payslip> {
    return this.http.get<Payslip>(`/api/employees/${employeeId}/payslip`, { params: new HttpParams().set('month', month) });
  }
}
