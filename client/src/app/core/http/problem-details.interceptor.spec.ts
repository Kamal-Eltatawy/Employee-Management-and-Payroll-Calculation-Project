import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationService } from '../notifications/notification.service';
import { ApiError } from './api-error';
import { problemDetailsInterceptor } from './problem-details.interceptor';

describe('problemDetailsInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let notifications: NotificationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([problemDetailsInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    notifications = TestBed.inject(NotificationService);
  });

  const request = (): Promise<ApiError> =>
    new Promise((resolve) => http.get('/api/departments').subscribe({ error: (error: ApiError) => resolve(error) }));

  it('reports an unreachable API (proxy 502) as a toast and rethrows an ApiError', async () => {
    const result = request();
    backend.expectOne('/api/departments').flush('', { status: 502, statusText: 'Bad Gateway' });

    expect((await result).status).toBe(502);
    expect(notifications.toasts().map((toast) => toast.kind)).toEqual(['error']);
  });

  it('leaves 4xx to the page, without a toast', async () => {
    const result = request();
    backend.expectOne('/api/departments').flush({ title: 'Conflict.', detail: 'Email is already in use.' }, { status: 409, statusText: 'Conflict' });

    expect((await result).detail).toBe('Email is already in use.');
    expect(notifications.toasts()).toEqual([]);
  });
});
