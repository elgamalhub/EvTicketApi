import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { apiError } from '../../core/format';
import { AuthShell } from '../../shared/auth-shell';
import { Icon } from '../../shared/icon';

type AccountKind = 'attendee' | 'organizer';

@Component({
  selector: 'app-register-page',
  imports: [FormsModule, RouterLink, AuthShell, Icon],
  templateUrl: './register-page.html',
  styleUrl: './auth-shared.scss'
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly fullName = signal('');
  readonly email = signal('');
  readonly phone = signal('');
  readonly password = signal('');
  readonly confirm = signal('');
  readonly kind = signal<AccountKind>('attendee');
  readonly accepted = signal(false);
  readonly showPassword = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    const role = this.route.snapshot.queryParamMap.get('role');
    if (role === 'organizer') this.kind.set('organizer');
  }

  readonly strength = computed(() => {
    const password = this.password();
    let score = 0;
    if (password.length >= 8) score++;
    if (password.length >= 12) score++;
    if (/[A-Z]/.test(password) && /[a-z]/.test(password)) score++;
    if (/\d/.test(password) && /[^A-Za-z0-9]/.test(password)) score++;
    return Math.min(4, score);
  });

  readonly strengthLabel = computed(() => {
    const labels = ['Too short', 'Weak', 'Fair', 'Good', 'Strong'];
    return labels[this.strength()];
  });

  readonly passwordsMatch = computed(
    () => this.confirm().length === 0 || this.confirm() === this.password()
  );

  readonly canSubmit = computed(
    () =>
      this.fullName().trim().length >= 2 &&
      /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(this.email().trim()) &&
      this.password().length >= 8 &&
      this.password() === this.confirm() &&
      this.accepted()
  );

  submit(): void {
    if (this.submitting()) return;

    if (!this.canSubmit()) {
      this.error.set(
        this.password() !== this.confirm()
          ? 'Passwords do not match.'
          : 'Fill in every required field and accept the terms.'
      );
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth
      .register({
        fullName: this.fullName().trim(),
        email: this.email().trim(),
        password: this.password(),
        phoneNumber: this.phone().trim() || null,
        asOrganizer: this.kind() === 'organizer'
      })
      .subscribe({
        next: (session) => {
          this.submitting.set(false);
          this.toast.success(
            'Account created',
            this.kind() === 'organizer'
              ? 'Your organizer studio is ready — create your first event.'
              : 'You are signed in. Find something worth going out for.'
          );
          void this.router.navigateByUrl(this.targetUrl(session.user.isOrganizer));
        },
        error: (error) => {
          this.submitting.set(false);
          this.error.set(apiError(error));
        }
      });
  }

  private targetUrl(isOrganizer: boolean): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl && returnUrl.startsWith('/')) return returnUrl;
    return isOrganizer ? '/organizer' : '/account';
  }
}
