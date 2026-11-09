import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../core/dashboard.service';
import { EventService } from '../../core/event.service';
import { ToastService } from '../../core/toast.service';
import { EventPerformance, OrganizerDashboard } from '../../core/models';
import { apiError, dayAndTime, money, number, statusLabel } from '../../core/format';
import { AreaChart } from '../../shared/area-chart';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-organizer-dashboard-page',
  imports: [RouterLink, AreaChart, Icon],
  templateUrl: './organizer-dashboard-page.html',
  styleUrl: './organizer-dashboard-page.scss'
})
export class OrganizerDashboardPage {
  private readonly dashboards = inject(DashboardService);
  private readonly eventsApi = inject(EventService);
  private readonly toast = inject(ToastService);

  readonly data = signal<OrganizerDashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly confirmingDelete = signal<string | null>(null);
  readonly deleting = signal<string | null>(null);

  readonly money = money;
  readonly number = number;
  readonly dayAndTime = dayAndTime;
  readonly statusLabel = statusLabel;

  readonly events = computed(() => this.data()?.events ?? []);
  readonly sales = computed(() => this.data()?.salesByDay ?? []);
  readonly recentBookings = computed(() => this.data()?.recentBookings ?? []);

  readonly bestEvent = computed(() => {
    const events = this.events();
    if (events.length === 0) return null;
    return events.reduce((best, event) => (event.revenue > best.revenue ? event : best), events[0]);
  });

  readonly checkedInTotal = computed(() => this.events().reduce((total, event) => total + event.checkedIn, 0));

  readonly hasData = computed(() => (this.data()?.totalEvents ?? 0) > 0);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.dashboards.organizer().subscribe({
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

  statusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'published':
      case 'soldout':
        return 'badge-success';
      case 'draft':
        return 'badge-muted';
      case 'completed':
        return 'badge-cyan';
      case 'cancelled':
        return 'badge-danger';
      default:
        return 'badge-muted';
    }
  }

  askDelete(id: string): void {
    this.confirmingDelete.set(id);
  }

  cancelDelete(): void {
    this.confirmingDelete.set(null);
  }

  removeEvent(event: EventPerformance): void {
    if (this.deleting()) return;
    this.deleting.set(event.id);

    this.eventsApi.delete(event.id).subscribe({
      next: () => {
        this.deleting.set(null);
        this.confirmingDelete.set(null);
        this.toast.success('Event deleted', `${event.title} was removed from your studio.`);
        this.load();
      },
      error: (error) => {
        this.deleting.set(null);
        this.toast.error('Could not delete the event', apiError(error));
      }
    });
  }
}
