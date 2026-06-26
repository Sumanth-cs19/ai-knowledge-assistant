import { Component } from '@angular/core';
import { ErrorStateComponent } from '../error-state/error-state.component';

@Component({
  selector: 'app-unauthorized',
  imports: [ErrorStateComponent],
  template: '<app-error-state statusCode="401" title="Sign In Required" message="Your session is missing or has expired." actionLink="/login" actionText="Go to Login" />'
})
export class UnauthorizedComponent {}
