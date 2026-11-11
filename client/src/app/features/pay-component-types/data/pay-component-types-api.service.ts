import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ReferenceDataApiService } from '../../employees/data/reference-data-api.service';
import { CreatePayComponentTypeRequest, PayComponentType, UpdatePayComponentTypeRequest } from './pay-component-type.models';

/** Thin typed wrapper over /api/pay-component-types. Every write drops the cached list used by the employee form. */
@Injectable({ providedIn: 'root' })
export class PayComponentTypesApiService {
  private readonly http = inject(HttpClient);
  private readonly referenceData = inject(ReferenceDataApiService);
  private readonly baseUrl = '/api/pay-component-types';

  list(): Observable<PayComponentType[]> {
    return this.http.get<PayComponentType[]>(this.baseUrl);
  }

  get(id: number): Observable<PayComponentType> {
    return this.http.get<PayComponentType>(`${this.baseUrl}/${id}`);
  }

  create(request: CreatePayComponentTypeRequest): Observable<PayComponentType> {
    return this.http.post<PayComponentType>(this.baseUrl, request).pipe(tap(() => this.referenceData.invalidatePayComponentTypes()));
  }

  update(id: number, request: UpdatePayComponentTypeRequest): Observable<PayComponentType> {
    return this.http
      .put<PayComponentType>(`${this.baseUrl}/${id}`, request)
      .pipe(tap(() => this.referenceData.invalidatePayComponentTypes()));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(tap(() => this.referenceData.invalidatePayComponentTypes()));
  }
}
