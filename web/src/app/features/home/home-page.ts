import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { EventCategory, EventSummary, Venue } from '../../core/models';
import { EventService } from '../../core/event.service';
import { apiError, dayAndTime, money, number, percent } from '../../core/format';
import { EventCard } from '../../shared/event-card';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, EventCard, Icon],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss'
})
export class HomePage {
  private readonly eventsApi = inject(EventService);
  private readonly router = inject(Router);

  readonly featured = signal<EventSummary[]>([]);
  readonly categories = signal<EventCategory[]>([]);
  readonly venues = signal<Venue[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly query = signal('');
  readonly city = signal('');

  readonly heroEvent = computed(() => this.featured()[0] ?? null);
  readonly upcoming = computed(() => this.featured().slice(1, 4));
  readonly totalSeats = computed(() => this.venues().reduce((sum, venue) => sum + venue.capacity, 0));
  readonly money = money;
  readonly number = number;
  readonly dayAndTime = dayAndTime;
  readonly percent = percent;

  constructor() {
    this.eventsApi.featured(7).subscribe({
      next: (events) => {
        this.featured.set(events);
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });

    this.eventsApi.categories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => undefined
    });

    this.eventsApi.venues().subscribe({
      next: (venues) => this.venues.set(venues),
      error: () => undefined
    });
  }

  search(): void {
    void this.router.navigate(['/events'], {
      queryParams: {
        q: this.query() || null,
        city: this.city() || null
      }
    });
  }

  seatsLeft(event: EventSummary): string {
    return event.availableTickets > 0 ? `${this.number(event.availableTickets)} seats left` : 'Sold out';
  }

  seatsFor(row: number): number[] {
    return Array.from({ length: row === 0 || row === 4 ? 10 : 12 }, (_, index) => index);
  }
}
