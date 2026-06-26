import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const toastr = inject(ToastrService);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
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
  if (typeof error.error?.detail === 'string') {
    return error.error.detail;
  }

  if (typeof error.error?.title === 'string') {
    return error.error.title;
  }

  return 'Please try again or contact support if the problem continues.';
}
