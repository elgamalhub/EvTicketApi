import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { apiError, initials } from '../../core/format';
import { AuthShell } from '../../shared/auth-shell';
import { Icon } from '../../shared/icon';

interface DemoAccount {
  label: string;
  email: string;
  role: string;
  note: string;
}

const DEMO_ACCOUNTS: DemoAccount[] = [
  { label: 'Alex Morgan', email: 'demo@evticket.dev', role: 'Attendee', note: 'Buy tickets, view QR codes' },
  { label: 'Jordan Blake', email: 'organizer@evticket.dev', role: 'Organizer', note: 'Run events and check-in' },
  { label: 'Sam Rivera', email: 'admin@evticket.dev', role: 'Admin', note: 'Full platform console' }
];

@Component({
  selector: 'app-login-page',
  imports: [FormsModule, RouterLink, AuthShell, Icon],
  templateUrl: './login-page.html',
  styleUrl: './auth-shared.scss'
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly email = signal('');
  readonly password = signal('');
  readonly showPassword = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly justReset = signal(false);
  readonly demoAccounts = DEMO_ACCOUNTS;
  readonly initials = initials;

  readonly canSubmit = computed(() => this.email().trim().length > 3 && this.password().length >= 8);

  constructor() {
    const params = this.route.snapshot.queryParamMap;
    const email = params.get('email');
    if (email) this.email.set(email);
    if (params.get('reset') === '1') this.justReset.set(true);
  }

  submit(): void {
    if (this.submitting()) return;
    const email = this.email().trim();
    const password = this.password();

    if (!email || !password) {
      this.error.set('Enter your email and password.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth.login(email, password).subscribe({
      next: (session) => {
        this.submitting.set(false);
        this.toast.success('Welcome back', `Signed in as ${session.user.fullName}.`);
        void this.router.navigateByUrl(this.targetUrl());
      },
      error: (error) => {
        this.submitting.set(false);
        this.error.set(apiError(error));
      }
    });
  }

  useDemo(account: DemoAccount): void {
    if (this.submitting()) return;
    this.email.set(account.email);
    this.password.set('Demo@12345');
    this.toast.info('Demo account filled', `${account.role} · ${account.email} · password Demo@12345`);
  }

  private targetUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl && returnUrl.startsWith('/')) return returnUrl;
    if (this.auth.isAdmin()) return '/admin';
    if (this.auth.isOrganizer()) return '/organizer';
    return '/account';
  }
}
