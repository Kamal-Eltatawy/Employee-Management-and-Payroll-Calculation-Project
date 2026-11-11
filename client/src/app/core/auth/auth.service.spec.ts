import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LoginResponse } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let backend: HttpTestingController;

  const response: LoginResponse = {
    accessToken: 'token',
    expiresAtUtc: '2999-01-01T00:00:00Z',
    roles: ['Hr', 'Employee'],
    employeeId: 3,
    email: 'hr@payroll.local',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('shares one refresh request between concurrent callers', () => {
    const received: string[] = [];
    auth.refresh().subscribe((r) => received.push(r.accessToken));
    auth.refresh().subscribe((r) => received.push(r.accessToken));

    backend.expectOne('/api/auth/refresh').flush(response);

    expect(received).toEqual(['token', 'token']);
    expect(auth.token()).toBe('token');
  });

  it('starts a new refresh request once the previous one has finished', () => {
    auth.refresh().subscribe();
    backend.expectOne('/api/auth/refresh').flush(response);

    auth.refresh().subscribe();
    backend.expectOne('/api/auth/refresh').flush(response);
  });

  it('treats a user with Hr among several roles as Hr', () => {
    auth.startSession(response);

    expect(auth.isHr()).toBe(true);
    expect(auth.canSeeOwnPayslip()).toBe(true);
  });

  it('accepts a single role string from the server', () => {
    auth.startSession({ ...response, roles: 'Employee' as unknown as LoginResponse['roles'] });

    expect(auth.currentUser()?.roles).toEqual(['Employee']);
    expect(auth.isHr()).toBe(false);
  });
});
