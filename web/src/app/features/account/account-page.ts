import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { BookingService } from '../../core/booking.service';
import { ToastService } from '../../core/toast.service';
import { BookingSummary } from '../../core/models';
import { apiError, dateShort, dayAndTime, initials, money, statusLabel } from '../../core/format';
import { Icon } from '../../shared/icon';

type AccountTab = 'tickets' | 'profile' | 'security';
type TicketFilter = 'upcoming' | 'past';

@Component({
  selector: 'app-account-page',
  imports: [FormsModule, RouterLink, Icon],
  templateUrl: './account-page.html',
  styleUrl: './account-page.scss'
})
export class AccountPage {
  private readonly auth = inject(AuthService);
  private readonly bookingsApi = inject(BookingService);
  private readonly toast = inject(ToastService);

  readonly user = this.auth.user;
  readonly isOrganizer = this.auth.isOrganizer;
  readonly isAdmin = this.auth.isAdmin;

  readonly tab = signal<AccountTab>('tickets');
  readonly ticketFilter = signal<TicketFilter>('upcoming');
  readonly bookings = signal<BookingSummary[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly fullName = signal(this.auth.user()?.fullName ?? '');
  readonly phone = signal(this.auth.user()?.phoneNumber ?? '');
  readonly avatarUrl = signal(this.auth.user()?.avatarUrl ?? '');
  readonly savingProfile = signal(false);

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly showPassword = signal(false);
  readonly savingPassword = signal(false);

  readonly initials = initials;
  readonly money = money;
  readonly dayAndTime = dayAndTime;
  readonly dateShort = dateShort;
  readonly statusLabel = statusLabel;

  private readonly now = Date.now();

  readonly upcoming = computed(() =>
    this.bookings()
      .filter(
        (booking) =>
          booking.status !== 'cancelled' &&
          booking.status !== 'expired' &&
          new Date(booking.eventStartsAt).getTime() >= this.now
      )
      .sort((a, b) => +new Date(a.eventStartsAt) - +new Date(b.eventStartsAt))
  );

  readonly past = computed(() =>
    this.bookings()
      .filter(
        (booking) =>
          booking.status === 'cancelled' ||
          booking.status === 'expired' ||
          new Date(booking.eventStartsAt).getTime() < this.now
      )
      .sort((a, b) => +new Date(b.eventStartsAt) - +new Date(a.eventStartsAt))
  );

  readonly visibleBookings = computed(() => (this.ticketFilter() === 'upcoming' ? this.upcoming() : this.past()));

  readonly memberSince = computed(() => {
    const created = this.user()?.createdAt;
    return created ? dateShort(created) : '—';
  });

  readonly profileValid = computed(() => {
    const avatar = this.avatarUrl().trim();
    const avatarOk = !avatar || /^https?:\/\/[^\s]+$/i.test(avatar);
    return this.fullName().trim().length >= 2 && avatarOk;
  });

  readonly passwordValid = computed(
    () =>
      this.currentPassword().length >= 8 &&
      this.newPassword().length >= 8 &&
      this.newPassword() === this.confirmPassword()
  );

  constructor() {
    this.loadBookings();
  }

  setTab(tab: AccountTab): void {
    this.tab.set(tab);
  }

  loadBookings(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.bookingsApi.mine(1, 50).subscribe({
      next: (result) => {
        this.bookings.set(result.items);
        this.loading.set(false);
      },
      error: (error) => {
        this.loadError.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  saveProfile(): void {
    if (!this.profileValid() || this.savingProfile()) return;
    this.savingProfile.set(true);

    this.auth
      .updateProfile({
        fullName: this.fullName().trim(),
        phoneNumber: this.phone().trim() || null,
        avatarUrl: this.avatarUrl().trim() || null
      })
      .subscribe({
        next: (user) => {
          this.savingProfile.set(false);
          this.toast.success('Profile saved', `Your details are up to date, ${user.fullName.split(' ')[0]}.`);
        },
        error: (error) => {
          this.savingProfile.set(false);
          this.toast.error('Could not save your profile', apiError(error));
        }
      });
  }

  savePassword(): void {
    if (!this.passwordValid() || this.savingPassword()) return;
    this.savingPassword.set(true);

    this.auth.changePassword(this.currentPassword(), this.newPassword()).subscribe({
      next: () => {
        this.savingPassword.set(false);
        this.currentPassword.set('');
        this.newPassword.set('');
        this.confirmPassword.set('');
        this.toast.success('Password changed', 'Use your new password next time you sign in.');
      },
      error: (error) => {
        this.savingPassword.set(false);
        this.toast.error('Could not change your password', apiError(error));
      }
    });
  }

  logout(): void {
    this.auth.logout();
  }
}
