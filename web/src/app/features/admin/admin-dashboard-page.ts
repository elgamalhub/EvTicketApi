import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../core/dashboard.service';
import { BookingService } from '../../core/booking.service';
import { ToastService } from '../../core/toast.service';
import { AdminBooking, AdminDashboard, AdminUser } from '../../core/models';
import {
  apiError,
  dateShort,
  dayAndTime,
  initials,
  money,
  number,
  statusLabel
} from '../../core/format';
import { AreaChart } from '../../shared/area-chart';
import { Icon } from '../../shared/icon';

type AdminTab = 'overview' | 'users' | 'bookings';

const BOOKING_STATUSES = ['all', 'confirmed', 'pending', 'cancelled', 'expired'];

@Component({
  selector: 'app-admin-dashboard-page',
  imports: [RouterLink, AreaChart, Icon],
  templateUrl: './admin-dashboard-page.html',
  styleUrl: './admin-dashboard-page.scss'
})
export class AdminDashboardPage {
  private readonly dashboards = inject(DashboardService);
  private readonly bookingsApi = inject(BookingService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly tab = signal<AdminTab>('overview');

  readonly data = signal<AdminDashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly users = signal<AdminUser[]>([]);
  readonly usersTotal = signal(0);
  readonly usersPage = signal(1);
  readonly usersTotalPages = signal(1);
  readonly usersLoading = signal(false);
  readonly usersError = signal<string | null>(null);
  readonly userQuery = signal('');
  readonly confirmingUser = signal<string | null>(null);
  readonly busyUser = signal<string | null>(null);

  readonly bookings = signal<AdminBooking[]>([]);
  readonly bookingsTotal = signal(0);
  readonly bookingsPage = signal(1);
  readonly bookingsTotalPages = signal(1);
  readonly bookingsLoading = signal(false);
  readonly bookingsError = signal<string | null>(null);
  readonly bookingQuery = signal('');
  readonly bookingStatus = signal('all');
  readonly bookingStatuses = BOOKING_STATUSES;

  readonly revenue = computed(() => this.data()?.revenueByDay ?? []);
  readonly categories = computed(() => this.data()?.bookingsByCategory ?? []);
  readonly topEvents = computed(() => this.data()?.topEvents ?? []);
  readonly recentUsers = computed(() => this.data()?.recentUsers ?? []);

  readonly maxCategory = computed(() =>
    this.categories().reduce((max, point) => Math.max(max, Number(point.value) || 0), 0)
  );

  readonly money = money;
  readonly number = number;
  readonly dateShort = dateShort;
  readonly dayAndTime = dayAndTime;
  readonly statusLabel = statusLabel;
  readonly initials = initials;

  private usersTimer?: ReturnType<typeof setTimeout>;
  private bookingsTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    this.load();

    this.destroyRef.onDestroy(() => {
      if (this.usersTimer) clearTimeout(this.usersTimer);
      if (this.bookingsTimer) clearTimeout(this.bookingsTimer);
    });
  }

  setTab(tab: AdminTab): void {
    this.tab.set(tab);
    if (tab === 'users' && this.users().length === 0 && !this.usersLoading()) {
      this.loadUsers(1);
    }
    if (tab === 'bookings' && this.bookings().length === 0 && !this.bookingsLoading()) {
      this.loadBookings(1);
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.dashboards.admin().subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  /* ---------- Users ---------- */

  loadUsers(page = 1): void {
    this.usersLoading.set(true);
    this.usersError.set(null);

    this.dashboards.users(page, 15, this.userQuery().trim() || undefined).subscribe({
      next: (result) => {
        this.users.set(result.items);
        this.usersTotal.set(result.total);
        this.usersPage.set(result.page);
        this.usersTotalPages.set(Math.max(1, result.totalPages));
        this.usersLoading.set(false);
      },
      error: (error) => {
        this.usersError.set(apiError(error));
        this.usersLoading.set(false);
      }
    });
  }

  onUserSearch(value: string): void {
    this.userQuery.set(value);
    if (this.usersTimer) clearTimeout(this.usersTimer);
    this.usersTimer = setTimeout(() => this.loadUsers(1), 350);
  }

  clearUserSearch(): void {
    if (this.usersTimer) clearTimeout(this.usersTimer);
    this.userQuery.set('');
    this.loadUsers(1);
  }

  prevUsersPage(): void {
    if (this.usersPage() <= 1) return;
    this.loadUsers(this.usersPage() - 1);
  }

  nextUsersPage(): void {
    if (this.usersPage() >= this.usersTotalPages()) return;
    this.loadUsers(this.usersPage() + 1);
  }

  toggleOrganizer(user: AdminUser): void {
    if (this.busyUser()) return;
    const promote = !user.isOrganizer;
    this.busyUser.set(user.id);

    this.dashboards.setOrganizer(user.id, promote).subscribe({
      next: () => {
        this.busyUser.set(null);
        this.users.update((items) =>
          items.map((item) => (item.id === user.id ? { ...item, isOrganizer: promote } : item))
        );
        this.toast.success(
          promote ? 'Organizer access granted' : 'Organizer access removed',
          `${user.fullName} can ${promote ? 'now' : 'no longer'} create and manage events.`
        );
        this.load();
      },
      error: (error) => {
        this.busyUser.set(null);
        this.toast.error('Could not update this account', apiError(error));
      }
    });
  }

  askDeleteUser(user: AdminUser): void {
    this.confirmingUser.set(user.id);
  }

  cancelDeleteUser(): void {
    this.confirmingUser.set(null);
  }

  deleteUser(user: AdminUser): void {
    if (this.busyUser()) return;
    this.busyUser.set(user.id);

    this.dashboards.deleteUser(user.id).subscribe({
      next: () => {
        this.busyUser.set(null);
        this.confirmingUser.set(null);
        this.toast.success('Account deleted', `${user.fullName}'s account and bookings were removed.`);
        this.loadUsers(this.usersPage());
        this.load();
      },
      error: (error) => {
        this.busyUser.set(null);
        this.toast.error('Could not delete this account', apiError(error));
      }
    });
  }

  /* ---------- Bookings ---------- */

  loadBookings(page = 1): void {
    this.bookingsLoading.set(true);
    this.bookingsError.set(null);

    const status = this.bookingStatus() === 'all' ? undefined : this.bookingStatus();
    this.bookingsApi.adminAll(page, 15, this.bookingQuery().trim() || undefined, status).subscribe({
      next: (result) => {
        this.bookings.set(result.items);
        this.bookingsTotal.set(result.total);
        this.bookingsPage.set(result.page);
        this.bookingsTotalPages.set(Math.max(1, result.totalPages));
        this.bookingsLoading.set(false);
      },
      error: (error) => {
        this.bookingsError.set(apiError(error));
        this.bookingsLoading.set(false);
      }
    });
  }

  onBookingSearch(value: string): void {
    this.bookingQuery.set(value);
    if (this.bookingsTimer) clearTimeout(this.bookingsTimer);
    this.bookingsTimer = setTimeout(() => this.loadBookings(1), 350);
  }

  clearBookingSearch(): void {
    if (this.bookingsTimer) clearTimeout(this.bookingsTimer);
    this.bookingQuery.set('');
    this.loadBookings(1);
  }

  setBookingStatus(status: string): void {
    if (this.bookingStatus() === status) return;
    this.bookingStatus.set(status);
    this.loadBookings(1);
  }

  prevBookingsPage(): void {
    if (this.bookingsPage() <= 1) return;
    this.loadBookings(this.bookingsPage() - 1);
  }

  nextBookingsPage(): void {
    if (this.bookingsPage() >= this.bookingsTotalPages()) return;
    this.loadBookings(this.bookingsPage() + 1);
  }

  exportBookings(): void {
    const rows = this.bookings();
    if (rows.length === 0) {
      this.toast.info('Nothing to export', 'No bookings on this page yet.');
      return;
    }

    const header = [
      'Reference',
      'Customer',
      'Email',
      'Event',
      'Tickets',
      'Total',
      'Status',
      'Payment',
      'Created'
    ];
    const lines = rows.map((booking) => [
      booking.reference,
      booking.customerName,
      booking.customerEmail,
      booking.eventTitle,
      booking.itemCount,
      booking.total,
      booking.status,
      booking.paymentStatus,
      booking.createdAt
    ]);

    const csv = [header, ...lines]
      .map((row) =>
        row
          .map((cell) => {
            const value = String(cell ?? '');
            return /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
          })
          .join(',')
      )
      .join('\n');

    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'evticket-bookings.csv';
    link.click();
    URL.revokeObjectURL(url);
    this.toast.success('Export ready', `${rows.length} bookings exported to CSV.`);
  }

  bookingStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'confirmed':
        return 'badge-success';
      case 'pending':
        return 'badge-warning';
      case 'cancelled':
      case 'expired':
        return 'badge-danger';
      default:
        return 'badge-muted';
    }
  }
}
