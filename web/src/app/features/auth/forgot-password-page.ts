import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { apiError } from '../../core/format';
import { AuthShell } from '../../shared/auth-shell';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-forgot-password-page',
  imports: [FormsModule, RouterLink, AuthShell, Icon],
  templateUrl: './forgot-password-page.html',
  styleUrl: './auth-shared.scss'
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);

  readonly email = signal('');
  readonly submitting = signal(false);
  readonly sent = signal(false);
  readonly error = signal<string | null>(null);

  readonly canSubmit = computed(
    () => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(this.email().trim()) && !this.submitting()
  );

  submit(): void {
    if (!this.canSubmit()) return;
    this.submitting.set(true);
    this.error.set(null);

    this.auth.forgotPassword(this.email().trim()).subscribe({
      next: () => {
        this.submitting.set(false);
        this.sent.set(true);
      },
      error: (error) => {
        this.submitting.set(false);
        this.error.set(apiError(error));
      }
    });
  }
}
