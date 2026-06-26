import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  protected readonly passwordValue = signal('');
  protected readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  }, {
    validators: [passwordsMatchValidator]
  });

  protected readonly passwordStrength = computed(() => this.calculatePasswordStrength(this.passwordValue()));
  protected readonly passwordStrengthLabel = computed(() => {
    const strength = this.passwordStrength();

    if (strength >= 80) {
      return 'Strong';
    }

    if (strength >= 50) {
      return 'Good';
    }

    if (strength >= 25) {
      return 'Weak';
    }

    return 'Too weak';
  });

  protected isSubmitting = false;
  protected backendErrors: string[] = [];

  constructor() {
    this.form.controls.password.valueChanges.subscribe((value) => this.passwordValue.set(value));
  }

  protected submit(): void {
    this.backendErrors = [];

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.isSubmitting = true;

    this.authService.register({ email, password }).pipe(
      finalize(() => {
        this.isSubmitting = false;
      })
    ).subscribe({
      next: () => {
        this.toastr.success('Your account is ready.', 'Registration complete');
        void this.router.navigate(['/dashboard']);
      },
      error: (error: unknown) => {
        this.backendErrors = this.extractErrors(error);
      }
    });
  }

  protected hasError(controlName: 'email' | 'password' | 'confirmPassword', errorName: string): boolean {
    const control = this.form.controls[controlName];
    return control.touched && control.hasError(errorName);
  }

  protected hasPasswordMismatch(): boolean {
    return this.form.controls.confirmPassword.touched && this.form.hasError('passwordMismatch');
  }

  private calculatePasswordStrength(password: string): number {
    let score = 0;

    if (password.length >= 8) {
      score += 25;
    }

    if (/[A-Z]/.test(password)) {
      score += 25;
    }

    if (/[0-9]/.test(password)) {
      score += 25;
    }

    if (/[^A-Za-z0-9]/.test(password)) {
      score += 25;
    }

    return score;
  }

  private extractErrors(error: unknown): string[] {
    if (!(error instanceof HttpErrorResponse)) {
      return ['Unable to create your account. Please try again.'];
    }

    const validationErrors = error.error?.errors as Record<string, string[]> | undefined;
    if (validationErrors) {
      return Object.values(validationErrors).flat();
    }

    if (typeof error.error?.detail === 'string') {
      return [error.error.detail];
    }

    return ['Unable to create your account. Please review the form and try again.'];
  }
}

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value as string | undefined;
  const confirmPassword = control.get('confirmPassword')?.value as string | undefined;

  return password && confirmPassword && password !== confirmPassword
    ? { passwordMismatch: true }
    : null;
}
