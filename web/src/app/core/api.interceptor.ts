import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injector } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

/**
 * Attaches the JWT to API calls, silently rotates refresh tokens on a 401 and
 * retries the failed request exactly once. All other errors are re-thrown for
 * the calling feature to present.
 */
export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  const injector = inject(Injector);
  const auth = injector.get(AuthService);

  const isApi = request.url.startsWith(environment.apiUrl) || request.url.startsWith('/api');
  const isAuthFlow = [
    '/auth/login',
    '/auth/register',
    '/auth/refresh',
    '/auth/forgot-password',
    '/auth/reset-password',
    '/auth/google'
  ].some((path) => request.url.includes(path));

  const token = auth.accessToken;
  const outgoing = token && isApi && !isAuthFlow
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      const unauthorized = error instanceof HttpErrorResponse && error.status === 401;
      const canRefresh = unauthorized && !isAuthFlow && !!token && !!auth.refreshToken;

      if (canRefresh) {
        return auth.refreshSession().pipe(
          switchMap((session) =>
            next(request.clone({ setHeaders: { Authorization: `Bearer ${session.accessToken}` } }))
          ),
          catchError((refreshError) => {
            auth.logout(true);
            return throwError(() => refreshError);
          })
        );
      }

      return throwError(() => error);
    })
  );
};
