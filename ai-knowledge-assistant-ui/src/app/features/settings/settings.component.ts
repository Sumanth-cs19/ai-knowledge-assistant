import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { ToastrService } from 'ngx-toastr';
import { Observable, map } from 'rxjs';

import { DEFAULT_USER_PREFERENCES, UserPreferences } from '../../core/models/preferences.model';
import { AuthService } from '../../core/services/auth.service';
import { PreferencesService } from '../../core/services/preferences.service';
import {
  UnsavedChangesDecision,
  UnsavedChangesDialogComponent
} from '../../shared/components/unsaved-changes-dialog/unsaved-changes-dialog.component';

@Component({
  selector: 'app-settings',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTabsModule
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent {
  protected readonly currentUser = inject(AuthService).currentUser;
  protected readonly displayName = computed(() => {
    const emailName = this.currentUser()?.email.split('@')[0] ?? '';
    if (!emailName) {
      return 'Not provided';
    }

    return emailName
      .split(/[._-]+/)
      .filter(Boolean)
      .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
      .join(' ');
  });
  protected readonly hasUnsavedChanges = signal(false);

  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly preferencesService = inject(PreferencesService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private savedPreferences: UserPreferences = { ...this.preferencesService.preferences() };

  protected readonly preferencesForm = this.formBuilder.group({
    defaultChatBehavior: [this.savedPreferences.defaultChatBehavior],
    streamingEnabled: [this.savedPreferences.streamingEnabled],
    compactSidebar: [this.savedPreferences.compactSidebar]
  });

  protected readonly passwordForm = this.formBuilder.group({
    oldPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  }, {
    validators: [passwordsMatchValidator]
  });

  constructor() {
    this.preferencesForm.valueChanges.subscribe(() => this.updateDirtyState());
  }

  @HostListener('window:beforeunload', ['$event'])
  protected preventRefreshWithUnsavedChanges(event: BeforeUnloadEvent): void {
    if (!this.hasUnsavedChanges()) {
      return;
    }

    event.preventDefault();
    event.returnValue = '';
  }

  canDeactivate(): boolean | Observable<boolean> {
    if (!this.hasUnsavedChanges()) {
      return true;
    }

    return this.dialog.open<UnsavedChangesDialogComponent, void, UnsavedChangesDecision>(
      UnsavedChangesDialogComponent,
      { disableClose: true, width: '420px' }
    ).afterClosed().pipe(map((decision) => {
      if (decision === 'save') {
        this.savePreferences();
        return true;
      }
      if (decision === 'discard') {
        this.discardPreferences(false);
        return true;
      }
      return false;
    }));
  }

  protected savePreferences(): void {
    const preferences = this.preferencesForm.getRawValue() as UserPreferences;
    this.preferencesService.updatePreferences(preferences);
    this.savedPreferences = { ...preferences };
    this.preferencesForm.markAsPristine();
    this.hasUnsavedChanges.set(false);
    this.toastr.success('Settings saved.', 'Preferences');
  }

  protected discardPreferences(showToast = true): void {
    this.preferencesForm.setValue(this.savedPreferences, { emitEvent: false });
    this.preferencesService.clearCompactSidebarPreview();
    this.preferencesForm.markAsPristine();
    this.hasUnsavedChanges.set(false);
    if (showToast) {
      this.toastr.info('Unsaved changes discarded.', 'Preferences');
    }
  }

  protected clearLocalSession(): void {
    this.discardPreferences(false);
    this.authService.clearSession();
    this.toastr.success('Local session cleared.', 'Session');
    void this.router.navigate(['/login']);
  }

  protected resetPreferences(): void {
    this.preferencesForm.setValue(DEFAULT_USER_PREFERENCES);
    this.toastr.info('Default preferences are ready to save.', 'Preferences');
  }

  protected validatePasswordForm(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.toastr.info('Password validation passed. Backend endpoint is not available yet.', 'Placeholder');
  }

  protected hasPasswordError(controlName: 'oldPassword' | 'newPassword' | 'confirmPassword', errorName: string): boolean {
    const control = this.passwordForm.controls[controlName];
    return control.touched && control.hasError(errorName);
  }

  protected hasPasswordMismatch(): boolean {
    return this.passwordForm.controls.confirmPassword.touched && this.passwordForm.hasError('passwordMismatch');
  }

  private updateDirtyState(): void {
    const current = this.preferencesForm.getRawValue();
    if (current.compactSidebar === this.savedPreferences.compactSidebar) {
      this.preferencesService.clearCompactSidebarPreview();
    } else {
      this.preferencesService.previewCompactSidebar(current.compactSidebar);
    }

    this.hasUnsavedChanges.set(
      current.defaultChatBehavior !== this.savedPreferences.defaultChatBehavior
      || current.streamingEnabled !== this.savedPreferences.streamingEnabled
      || current.compactSidebar !== this.savedPreferences.compactSidebar
    );
  }
}

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword')?.value as string | undefined;
  const confirmPassword = control.get('confirmPassword')?.value as string | undefined;

  return password && confirmPassword && password !== confirmPassword
    ? { passwordMismatch: true }
    : null;
}
