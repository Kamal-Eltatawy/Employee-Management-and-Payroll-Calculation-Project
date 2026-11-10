import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, shareReplay, tap } from 'rxjs';
import { CurrentUser, LoginResponse, homeUrlFor, isExpired, parseStoredUser } from './auth.models';

const STORAGE_KEY = 'payroll.session';

/**
 * The only shared client state (D41). The short-lived access token lives in sessionStorage plus this signal (D45);
 * the refresh token is an HttpOnly cookie the browser sends only to /api/auth (D55). The server remains the authority.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly user = signal<CurrentUser | null>(parseStoredUser(sessionStorage.getItem(STORAGE_KEY)));
  private refreshInFlight: Observable<LoginResponse> | null = null;

  readonly currentUser = this.user.asReadonly();
  readonly isHr = computed(() => this.user()?.role === 'Hr');

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(tap((response) => this.store(email.trim(), response)));
  }

  /** Renews the access token with the refresh cookie. Concurrent callers share one request (rotation allows one use). */
  refresh(): Observable<LoginResponse> {
    this.refreshInFlight ??= this.http.post<LoginResponse>('/api/auth/refresh', {}).pipe(
      tap((response) => this.store(this.user()?.email ?? '', response)),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /** Revokes the session on the server (best effort), then forgets it locally. */
  logout(): void {
    this.http.post('/api/auth/logout', {}).subscribe({ error: () => undefined });
    this.endSession();
  }

  /** Local sign-out only, used when the server has already rejected the session. */
  endSession(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.user.set(null);
    void this.router.navigate(['/login']);
  }

  /** The bearer token, or null when signed out or expired. */
  token(): string | null {
    const user = this.user();
    return user && !isExpired(user) ? user.accessToken : null;
  }

  homeUrl(): string {
    return homeUrlFor(this.user());
  }

  private store(email: string, response: LoginResponse): void {
    const user: CurrentUser = { email, ...response };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(user));
    this.user.set(user);
  }
}
