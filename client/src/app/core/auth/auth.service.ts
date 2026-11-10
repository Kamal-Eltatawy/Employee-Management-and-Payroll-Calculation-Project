import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, of, shareReplay, tap } from 'rxjs';
import { CurrentUser, LoginResponse, homeUrlFor, isExpired } from './auth.models';

/**
 * The only shared client state (D41). The server is the single source of the session (D61):
 * the refresh token is an HttpOnly cookie shared by every tab and by both language builds,
 * and the short-lived access token lives in memory only. On start-up the app asks the server who is signed in,
 * so switching language never asks for a new login and every instance shows the same user.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly user = signal<CurrentUser | null>(null);
  private refreshInFlight: Observable<LoginResponse> | null = null;

  readonly currentUser = this.user.asReadonly();
  readonly isHr = computed(() => this.user()?.role === 'Hr');

  /** App initializer: restore the session from the refresh cookie, if there is one. Never fails. */
  restoreSession(): Observable<unknown> {
    return this.refresh().pipe(catchError(() => of(null)));
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(tap((response) => this.startSession(response)));
  }

  /** Renews the access token with the refresh cookie. Concurrent callers share one request. */
  refresh(): Observable<LoginResponse> {
    this.refreshInFlight ??= this.http.post<LoginResponse>('/api/auth/refresh', {}).pipe(
      tap((response) => this.startSession(response)),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /** Ends the session on the server (for every tab and language), then locally. */
  logout(): void {
    this.http.post('/api/auth/logout', {}).subscribe({ error: () => undefined });
    this.endSession();
  }

  /** Local sign-out only, used when the server has already rejected the session. */
  endSession(): void {
    this.user.set(null);
    void this.router.navigate(['/login']);
  }

  startSession(response: LoginResponse): void {
    this.user.set(response);
  }

  /** The bearer token, or null when signed out or expired. */
  token(): string | null {
    const user = this.user();
    return user && !isExpired(user) ? user.accessToken : null;
  }

  homeUrl(): string {
    return homeUrlFor(this.user());
  }
}
