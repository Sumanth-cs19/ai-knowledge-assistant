import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';

export type UnsavedChangesDecision = 'save' | 'discard' | 'cancel';

@Component({
  selector: 'app-unsaved-changes-dialog',
  imports: [MatButtonModule, MatDialogModule],
  templateUrl: './unsaved-changes-dialog.component.html'
})
export class UnsavedChangesDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<UnsavedChangesDialogComponent, UnsavedChangesDecision>);

  protected close(decision: UnsavedChangesDecision): void {
    this.dialogRef.close(decision);
  }
}
