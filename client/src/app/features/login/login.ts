import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { StatusPanelComponent } from '../../shared/status-panel.component';
import { AuthIntegrationGapError, AuthService } from '../../core/auth/auth.service';

@Component({ selector: 'app-login', standalone: true, imports: [ReactiveFormsModule, StatusPanelComponent], templateUrl: './login.html', styleUrl: './login.scss' })
export class LoginPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly form = this.formBuilder.nonNullable.group({ identifier: ['', [Validators.required]], password: ['', [Validators.required, Validators.minLength(8)]] });
  loading = false;
  errorMessage: string | null = null;
  showPassword = false;

  submit(): void {
    this.errorMessage = null;
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    const { identifier, password } = this.form.getRawValue();
    this.auth.login(identifier, password).subscribe({
      next: () => { this.loading = false; void this.router.navigateByUrl(this.safeReturnUrl()); },
      error: (error: unknown) => { this.loading = false; this.errorMessage = error instanceof AuthIntegrationGapError ? error.message : 'Sign-in failed. Try again or contact support.'; }
    });
  }

  safeReturnUrl(candidate = this.route.snapshot.queryParamMap.get('returnUrl')): string {
    return candidate && candidate.startsWith('/') && !candidate.startsWith('//') ? candidate : '/app';
  }
}
