import { Injectable } from '@angular/core';

import { appEnvironment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ClientLoggerService {
  error(message: string, error?: unknown): void {
    if (!appEnvironment.production) {
      console.error(message, error);
    }
  }

  warn(message: string, context?: unknown): void {
    if (!appEnvironment.production) {
      console.warn(message, context);
    }
  }
}
