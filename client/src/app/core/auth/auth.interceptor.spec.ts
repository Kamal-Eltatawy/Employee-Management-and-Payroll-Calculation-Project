import '@angular/localize/init';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../http/problem-details.interceptor';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'login', children: [] }]),
        provideHttpClient(withInterceptors([authInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    auth.startSession({ email: 'hr@payroll.local', role: 'Hr', employeeId: null, accessToken: 'old-token', expiresAtUtc: '2999-01-01T00:00:00Z' });
  });

  it('sends the bearer token to the API', () => {
    http.get('/api/employees').subscribe();

    expect(backend.expectOne('/api/employees').request.headers.get('Authorization')).toBe('Bearer old-token');
  });

  it('on 401 renews the token once and retries the request', () => {
    let body: unknown;
    http.get('/api/employees').subscribe((value) => (body = value));

    backend.expectOne('/api/employees').flush({ title: 'Unauthorized.' }, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/auth/refresh').flush({
      accessToken: 'new-token', expiresAtUtc: '2999-01-01T00:00:00Z', role: 'Hr', employeeId: null, email: 'hr@payroll.local',
    });
    const retry = backend.expectOne('/api/employees');
    retry.flush({ ok: true });

    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    expect(body).toEqual({ ok: true });
    expect(auth.token()).toBe('new-token');
  });

  it('ends the session when the refresh is rejected', () => {
    let failed = false;
    http.get('/api/employees').subscribe({ error: () => (failed = true) });

    backend.expectOne('/api/employees').flush({ title: 'Unauthorized.' }, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/auth/refresh').flush({ title: 'Unauthorized.' }, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    expect(auth.currentUser()).toBeNull();
  });
});
