import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../notifications/notification.service';
import { isReportedGlobally, toApiError } from './api-error';

/**
 * Every HTTP failure leaves HttpClient as a typed ApiError (D42).
 * Network failures and server errors are also reported globally; pages handle 4xx themselves.
 * 401 is left to authInterceptor (refresh and retry, D55).
 */
export const problemDetailsInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((error: unknown) => {
      const apiError = toApiError(error);
      if (isReportedGlobally(apiError)) {
        notifications.error(apiError.detail ?? apiError.title);
      }

      return throwError(() => apiError);
    }),
  );
};
