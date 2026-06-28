import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';

import { ClientLoggerService } from '../services/client-logger.service';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const toastr = inject(ToastrService);
  const router = inject(Router);
  const logger = inject(ClientLoggerService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        logger.error(`API request failed: ${request.method} ${request.url}`, {
          status: error.status,
          statusText: error.statusText,
          error: error.error
        });
        const message = getErrorMessage(error);

        if (error.status !== 401 || !request.url.includes('/auth/refresh')) {
          toastr.error(message, getErrorTitle(error.status));
        }

        if (error.status === 403) {
          void router.navigate(['/forbidden']);
        } else if (error.status >= 500) {
          void router.navigate(['/server-error']);
        }
      }

      return throwError(() => error);
    })
  );
};

function getErrorTitle(status: number): string {
  switch (status) {
    case 0:
      return 'Connection failed';
    case 400:
      return 'Invalid request';
    case 401:
      return 'Authentication required';
    case 403:
      return 'Access denied';
    case 404:
      return 'Not found';
    case 500:
      return 'Server error';
    default:
      return 'Request failed';
  }
}

function getErrorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'The API could not be reached. Check your network connection and try again.';
  }

  if (typeof error.error?.detail === 'string') {
    return error.error.detail;
  }

  if (typeof error.error?.title === 'string') {
    return error.error.title;
  }

  return 'Please try again or contact support if the problem continues.';
}
