import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, finalize, Observable, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthSession, AuthUser, LoginRequest, RegisterRequest } from './models';

const STORAGE_KEY = 'evticket.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = environment.apiUrl;

  private readonly _session = signal<AuthSession | null>(this.readSession());
  readonly session = this._session.asReadonly();
  readonly user = computed(() => this._session()?.user ?? null);
  readonly isAuthenticated = computed(() => !!this._session()?.accessToken);
  readonly isAdmin = computed(() => this.hasRole('Admin'));
  readonly isOrganizer = computed(() => {
    const user = this.user();
    return !!user && (user.isOrganizer || user.roles.includes('Organizer') || user.roles.includes('Admin'));
  });

  private refreshRequest$?: Observable<AuthSession>;

  get accessToken(): string | null {
    return this._session()?.accessToken ?? null;
  }

  get refreshToken(): string | null {
    return this._session()?.refreshToken ?? null;
  }

  login(email: string, password: string): Observable<AuthSession> {
    const request: LoginRequest = { email, password };
    return this.http
      .post<AuthSession>(`${this.api}/api/auth/login`, request)
      .pipe(tap((session) => this.setSession(session)));
  }

  register(request: RegisterRequest): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(`${this.api}/api/auth/register`, request)
      .pipe(tap((session) => this.setSession(session)));
  }

  demoLogin(email: string): Observable<AuthSession> {
    return this.login(email, 'Demo@12345');
  }

  refreshSession(): Observable<AuthSession> {
    const refreshToken = this.refreshToken;
    if (!refreshToken) return throwError(() => new Error('No refresh token available'));

    if (!this.refreshRequest$) {
      this.refreshRequest$ = this.http
        .post<AuthSession>(`${this.api}/api/auth/refresh`, { refreshToken })
        .pipe(
          tap((session) => this.setSession(session)),
          catchError((error) => {
            this.clearSession();
            return throwError(() => error);
          }),
          finalize(() => {
            this.refreshRequest$ = undefined;
          }),
          shareReplay({ bufferSize: 1, refCount: false })
        );
    }
    return this.refreshRequest$;
  }

  updateProfile(payload: { fullName: string; phoneNumber?: string | null; avatarUrl?: string | null }): Observable<AuthUser> {
    return this.http
      .put<AuthUser>(`${this.api}/api/auth/me`, payload)
      .pipe(tap((user) => this.patchUser(user)));
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.api}/api/auth/change-password`, { currentPassword, newPassword });
  }

  forgotPassword(email: string): Observable<unknown> {
    return this.http.post(`${this.api}/api/auth/forgot-password`, { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.api}/api/auth/reset-password`, { email, token, newPassword });
  }

  logout(navigate = true): void {
    const refreshToken = this.refreshToken;
    if (refreshToken) {
      this.http.post(`${this.api}/api/auth/logout`, { refreshToken }).subscribe({ error: () => undefined });
    }
    this.clearSession();
    if (navigate) void this.router.navigate(['/']);
  }

  hasRole(role: string): boolean {
    return this.user()?.roles.includes(role) ?? false;
  }

  private setSession(session: AuthSession): void {
    this._session.set(session);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // storage can be unavailable in private mode — session still lives in memory
    }
  }

  private patchUser(user: AuthUser): void {
    const session = this._session();
    if (!session) return;
    this.setSession({ ...session, user });
  }

  private clearSession(): void {
    this._session.set(null);
    this.refreshRequest$ = undefined;
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }

  private readSession(): AuthSession | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as AuthSession;
      if (!parsed?.accessToken || !parsed?.refreshToken) return null;
      return parsed;
    } catch {
      return null;
    }
  }
}
