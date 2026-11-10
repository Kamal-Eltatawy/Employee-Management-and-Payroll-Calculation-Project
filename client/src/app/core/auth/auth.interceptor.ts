import { HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { ApiError } from '../http/api-error';
import { AuthService } from './auth.service';

const isAuthCall = (url: string) => url.startsWith('/api/auth/');

/**
 * Adds the bearer token to API calls. On a 401 it renews the access token once with the refresh cookie and
 * retries; if renewal fails, the session is over (D55).
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  if (!request.url.startsWith('/api/') || isAuthCall(request.url)) {
    return next(request);
  }

  return send(request, next, auth.token()).pipe(
    catchError((error: ApiError) => {
      if (error.status !== 401 || !auth.currentUser()) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError(() => {
          auth.endSession();
          return throwError(() => error);
        }),
        switchMap((renewed) => send(request, next, renewed.accessToken)),
      );
    }),
  );
};

function send(request: HttpRequest<unknown>, next: HttpHandlerFn, token: string | null) {
  return next(token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request);
}
