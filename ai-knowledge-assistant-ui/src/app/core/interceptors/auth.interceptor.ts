import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';

import { APP_CONFIG } from '../constants/app.constants';
import { AuthResponse } from '../models/auth.model';
import { AuthService } from '../services/auth.service';

let isRefreshing = false;
const refreshSubject = new BehaviorSubject<AuthResponse | null>(null);

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const accessToken = authService.getAccessToken();
  const isAuthEndpoint = request.url.startsWith(`${APP_CONFIG.apiBaseUrl}/auth/`);

  const authRequest = accessToken && !isAuthEndpoint
    ? request.clone({
      setHeaders: {
        Authorization: `Bearer ${accessToken}`
      }
    })
    : request;

  return next(authRequest).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      if (!authService.getRefreshToken()) {
        authService.clearSession();
        void router.navigate(['/login']);
        return throwError(() => error);
      }

      if (isRefreshing) {
        return refreshSubject.pipe(
          filter((response): response is AuthResponse => response !== null),
          take(1),
          switchMap((response) => next(request.clone({
            setHeaders: {
              Authorization: `Bearer ${response.accessToken}`
            }
          })))
        );
      }

      isRefreshing = true;
      refreshSubject.next(null);

      return authService.refreshToken().pipe(
        switchMap((response) => {
          isRefreshing = false;
          refreshSubject.next(response);

          return next(request.clone({
            setHeaders: {
              Authorization: `Bearer ${response.accessToken}`
            }
          }));
        }),
        catchError((refreshError: unknown) => {
          isRefreshing = false;
          refreshSubject.next(null);
          authService.clearSession();
          void router.navigate(['/login']);
          return throwError(() => refreshError);
        })
      );
    })
  );
};
