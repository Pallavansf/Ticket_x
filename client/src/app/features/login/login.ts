import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { StatusPanelComponent } from '../../shared/status-panel.component';
import { AuthService } from '../../core/auth/auth.service';

@Component({ selector: 'app-login', standalone: true, imports: [ReactiveFormsModule, StatusPanelComponent], templateUrl: './login.html', styleUrl: './login.scss' })
export class LoginPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly form = this.formBuilder.nonNullable.group({ identifier: ['', [Validators.required]], password: ['', [Validators.required, Validators.minLength(8)]] });
  readonly rotationForm = this.formBuilder.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(1024)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(1024)]]
  });
  loading = false;
  errorMessage: string | null = null;
  showPassword = false;
  requiresPasswordRotation = false;
  private currentPassword = '';

  submit(): void {
    this.errorMessage = null;
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    const { identifier, password } = this.form.getRawValue();
    this.auth.login(identifier, password).subscribe({
      next: () => {
        this.loading = false;
        if (this.auth.session()?.passwordRotationRequired) {
          this.currentPassword = password;
          this.requiresPasswordRotation = true;
          return;
        }
        void this.router.navigateByUrl(this.safeReturnUrl());
      },
      error: () => { this.loading = false; this.errorMessage = 'Sign-in failed. Check your login name and password or contact support.'; }
    });
  }

  changePassword(): void {
    this.errorMessage = null;
    if (this.rotationForm.invalid) { this.rotationForm.markAllAsTouched(); return; }
    const { newPassword, confirmPassword } = this.rotationForm.getRawValue();
    if (newPassword !== confirmPassword) {
      this.errorMessage = 'The new password and confirmation do not match.';
      return;
    }
    this.loading = true;
    this.auth.changePassword(this.currentPassword, newPassword).subscribe({
      next: () => {
        this.loading = false;
        this.currentPassword = '';
        this.auth.logout();
        this.form.controls.password.reset('');
        void this.router.navigateByUrl('/login');
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'The password could not be changed. Verify the current password and try again.';
      }
    });
  }

  safeReturnUrl(candidate = this.route.snapshot.queryParamMap.get('returnUrl')): string {
    return candidate && candidate.startsWith('/') && !candidate.startsWith('//') ? candidate : '/app';
  }
}
