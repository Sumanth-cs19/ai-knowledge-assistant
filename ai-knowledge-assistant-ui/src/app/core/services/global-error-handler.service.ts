import { ErrorHandler, Injectable, inject } from '@angular/core';

import { ClientLoggerService } from './client-logger.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly logger = inject(ClientLoggerService);

  handleError(error: unknown): void {
    this.logger.error('Unhandled client error', error);
  }
}
