import { Component } from '@angular/core';
import { ConfirmationDialogComponent } from '../confirmation-dialog/confirmation-dialog.component';

@Component({
  selector: 'app-delete-dialog',
  imports: [ConfirmationDialogComponent],
  template: '<app-confirmation-dialog />'
})
export class DeleteDialogComponent {}
