import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';

import { SKIP_GLOBAL_LOADING } from '../constants/http-context.constants';
import { LoadingService } from '../services/loading.service';

export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(SKIP_GLOBAL_LOADING)) {
    return next(request);
  }

  const loadingService = inject(LoadingService);

  loadingService.show();

  return next(request).pipe(
    finalize(() => loadingService.hide())
  );
};
