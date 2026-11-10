import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Employee, EmployeeListItem, EmployeeQuery, EmployeeRequest, PagedResult, UpdateEmployeeRequest } from './employee.models';

/** Thin typed wrapper over /api/employees (D41). */
@Injectable({ providedIn: 'root' })
export class EmployeesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/employees';

  search(query: EmployeeQuery): Observable<PagedResult<EmployeeListItem>> {
    let params = new HttpParams()
      .set('sortBy', query.sortBy)
      .set('sortDir', query.sortDir)
      .set('page', query.page)
      .set('pageSize', query.pageSize);
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.departmentId) {
      params = params.set('departmentId', query.departmentId);
    }
    if (query.status) {
      params = params.set('status', query.status);
    }

    return this.http.get<PagedResult<EmployeeListItem>>(this.baseUrl, { params });
  }

  get(id: number): Observable<Employee> {
    return this.http.get<Employee>(`${this.baseUrl}/${id}`);
  }

  create(request: EmployeeRequest): Observable<Employee> {
    return this.http.post<Employee>(this.baseUrl, request);
  }

  update(id: number, request: UpdateEmployeeRequest): Observable<Employee> {
    return this.http.put<Employee>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
