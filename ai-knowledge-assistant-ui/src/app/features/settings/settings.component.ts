import { Component, inject } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { ToastrService } from 'ngx-toastr';

import { APP_CONFIG } from '../../core/constants/app.constants';
import { UserPreferences } from '../../core/models/preferences.model';
import { AuthService } from '../../core/services/auth.service';
import { PreferencesService } from '../../core/services/preferences.service';

@Component({
  selector: 'app-settings',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatRadioModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTabsModule
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly preferencesService = inject(PreferencesService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  protected readonly preferences = this.preferencesService.preferences;
  protected readonly apiBaseUrl = APP_CONFIG.apiBaseUrl;

  protected readonly preferencesForm = this.formBuilder.group({
    theme: [this.preferences().theme],
    defaultChatBehavior: [this.preferences().defaultChatBehavior],
    streamingEnabled: [this.preferences().streamingEnabled],
    compactSidebar: [this.preferences().compactSidebar]
  });

  protected readonly passwordForm = this.formBuilder.group({
    oldPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  }, {
    validators: [passwordsMatchValidator]
  });

  protected savePreferences(): void {
    const preferences = this.preferencesForm.getRawValue() as UserPreferences;
    this.preferencesService.updatePreferences(preferences);
    this.toastr.success('Settings saved.', 'Preferences');
  }

  protected themeChanged(): void {
    this.preferencesService.updateTheme(this.preferencesForm.controls.theme.value);
    this.toastr.success('Theme changed.', 'Appearance');
  }

  protected clearLocalSession(): void {
    this.authService.clearSession();
    this.toastr.success('Local session cleared.', 'Session');
    void this.router.navigate(['/login']);
  }

  protected resetPreferences(): void {
    this.preferencesService.resetPreferences();
    this.preferencesForm.setValue(this.preferences());
    this.toastr.success('Local preferences reset.', 'Preferences');
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
}

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword')?.value as string | undefined;
  const confirmPassword = control.get('confirmPassword')?.value as string | undefined;

  return password && confirmPassword && password !== confirmPassword
    ? { passwordMismatch: true }
    : null;
}
