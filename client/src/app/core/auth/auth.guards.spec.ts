import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Observable, firstValueFrom, isObservable } from 'rxjs';
import { authGuard, guestGuard, roleGuard } from './auth.guards';
import { LoginResponse, Role } from './auth.models';
import { AuthService } from './auth.service';

describe('auth guards', () => {
  let auth: AuthService;
  let backend: HttpTestingController;
  let router: Router;

  const session = (roles: Role[], expiresAtUtc = '2999-01-01T00:00:00Z', employeeId: number | null = null): LoginResponse => ({
    accessToken: 'token',
    expiresAtUtc,
    roles,
    employeeId,
    email: 'user@payroll.local',
  });

  const run = (guard: CanActivateFn, url = '/employees') =>
    runInInjectionContext(TestBed.inject(EnvironmentInjector), () =>
      guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );

  const serialize = (result: unknown) => (result instanceof UrlTree ? router.serializeUrl(result) : result);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => backend.verify());

  describe('authGuard', () => {
    it('lets a user with a valid access token through', () => {
      auth.startSession(session(['Hr']));

      expect(run(authGuard)).toBe(true);
    });

    it('sends a signed-out user to login with the return URL', () => {
      expect(serialize(run(authGuard))).toBe('/login?returnUrl=%2Femployees');
    });

    it('renews an expired access token before letting the user through', async () => {
      auth.startSession(session(['Hr'], '2000-01-01T00:00:00Z'));
      const result = run(authGuard);
      expect(isObservable(result)).toBe(true);

      const outcome = firstValueFrom(result as Observable<unknown>);
      backend.expectOne('/api/auth/refresh').flush(session(['Hr']));

      expect(await outcome).toBe(true);
    });

    it('sends the user to login when the renewal fails', async () => {
      auth.startSession(session(['Hr'], '2000-01-01T00:00:00Z'));
      const outcome = firstValueFrom(run(authGuard) as Observable<unknown>);
      backend.expectOne('/api/auth/refresh').flush({ title: 'Unauthorized.' }, { status: 401, statusText: 'Unauthorized' });

      expect(serialize(await outcome)).toBe('/login?returnUrl=%2Femployees');
    });
  });

  describe('guestGuard', () => {
    it('lets a signed-out user open the login page', () => {
      expect(run(guestGuard, '/login')).toBe(true);
    });

    it('sends a signed-in user to the home page', () => {
      auth.startSession(session(['Employee'], '2999-01-01T00:00:00Z', 1));

      expect(serialize(run(guestGuard, '/login'))).toBe('/home');
    });
  });

  describe('roleGuard', () => {
    it('accepts a user who has the role among several', () => {
      auth.startSession(session(['Employee', 'Hr'], '2999-01-01T00:00:00Z', 1));

      expect(run(roleGuard('Hr'))).toBe(true);
      expect(run(roleGuard('Employee'))).toBe(true);
    });

    it('sends a user without the role to the home page', () => {
      auth.startSession(session(['Employee'], '2999-01-01T00:00:00Z', 1));

      expect(serialize(run(roleGuard('Hr')))).toBe('/home');
    });
  });
});
