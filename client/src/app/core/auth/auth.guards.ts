import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { Role, hasRole } from './auth.models';
import { AuthService } from './auth.service';

/** UX only: the server enforces every rule (§13.2). An expired access token is renewed before giving up (D55). */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const loginUrl = inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

  if (auth.token()) {
    return true;
  }

  if (!auth.currentUser()) {
    return loginUrl;
  }

  return auth.refresh().pipe(
    map(() => true),
    catchError(() => of(loginUrl)),
  );
};

/** The login page is for signed-out users only; a signed-in user goes to their home page. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.currentUser() ? inject(Router).parseUrl(auth.homeUrl()) : true;
};

/** Passes when the user has the role among their roles (D67: a user may have several). */
export function roleGuard(role: Role): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    return hasRole(auth.currentUser(), role) ? true : inject(Router).parseUrl(auth.homeUrl());
  };
}
