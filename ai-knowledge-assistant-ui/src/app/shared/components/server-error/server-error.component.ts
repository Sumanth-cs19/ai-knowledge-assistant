import { Component } from '@angular/core';
import { ErrorStateComponent } from '../error-state/error-state.component';

@Component({
  selector: 'app-server-error',
  imports: [ErrorStateComponent],
  template: '<app-error-state statusCode="500" title="Server Error" message="The service could not complete the request." />'
})
export class ServerErrorComponent {}
