import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const toastr = inject(ToastrService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const message = getErrorMessage(error);

        if (error.status !== 401 || !request.url.includes('/auth/refresh')) {
          toastr.error(message, getErrorTitle(error.status));
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
