import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { apiError } from '../../core/format';
import { AuthShell } from '../../shared/auth-shell';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-reset-password-page',
  imports: [FormsModule, RouterLink, AuthShell, Icon],
  templateUrl: './reset-password-page.html',
  styleUrl: './auth-shared.scss'
})
export class ResetPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly email = signal('');
  readonly token = signal('');
  readonly password = signal('');
  readonly confirm = signal('');
  readonly showPassword = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    const params = this.route.snapshot.queryParamMap;
    this.email.set(params.get('email') ?? '');
    this.token.set(params.get('token') ?? '');
  }

  readonly hasLink = computed(() => !!this.email() && !!this.token());

  readonly strength = computed(() => {
    const password = this.password();
    let score = 0;
    if (password.length >= 8) score++;
    if (password.length >= 12) score++;
    if (/[A-Z]/.test(password) && /[a-z]/.test(password)) score++;
    if (/\d/.test(password) && /[^A-Za-z0-9]/.test(password)) score++;
    return Math.min(4, score);
  });

  readonly strengthLabel = computed(() => ['Too short', 'Weak', 'Fair', 'Good', 'Strong'][this.strength()]);

  readonly canSubmit = computed(
    () =>
      this.hasLink() &&
      this.password().length >= 8 &&
      this.password() === this.confirm() &&
      !this.submitting()
  );

  submit(): void {
    if (!this.canSubmit()) return;
    this.submitting.set(true);
    this.error.set(null);

    this.auth.resetPassword(this.email(), this.token(), this.password()).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toast.success('Password updated', 'Sign in with your new password.');
        void this.router.navigate(['/login'], { queryParams: { reset: 1, email: this.email() } });
      },
      error: (error) => {
        this.submitting.set(false);
        this.error.set(apiError(error));
      }
    });
  }
}
