import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EventDetail, EventSummary, SeatMap, SeatStatus } from '../../core/models';
import { EventService } from '../../core/event.service';
import { ToastService } from '../../core/toast.service';
import { apiError, dateLong, dayAndTime, money, number, percent, timeOfDay } from '../../core/format';
import { EventCard } from '../../shared/event-card';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-event-detail-page',
  imports: [RouterLink, EventCard, Icon],
  templateUrl: './event-detail-page.html',
  styleUrl: './event-detail-page.scss'
})
export class EventDetailPage {
  private readonly eventsApi = inject(EventService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly event = signal<EventDetail | null>(null);
  readonly seatMap = signal<SeatMap | null>(null);
  readonly related = signal<EventSummary[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly paragraphs = computed(() =>
    (this.event()?.description ?? '')
      .split(/\n{2,}/)
      .map((paragraph) => paragraph.trim())
      .filter(Boolean)
  );

  readonly previewSeats = computed(() => {
    const map = this.seatMap();
    if (!map) return [];
    return map.sections.flatMap((section) =>
      section.seats.map((seat) => ({ ...seat, color: section.color }))
    );
  });

  readonly sellThrough = computed(() => {
    const event = this.event();
    return event ? percent(event.ticketsSold, event.capacity) : 0;
  });

  readonly sellThroughRounded = computed(() => Math.round(this.sellThrough()));

  readonly dateLong = dateLong;
  readonly dayAndTime = dayAndTime;
  readonly timeOfDay = timeOfDay;
  readonly money = money;
  readonly number = number;

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug');
      if (!slug) return;
      this.load(slug);
    });
  }

  private load(slug: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.seatMap.set(null);
    this.related.set([]);

    this.eventsApi.get(slug).subscribe({
      next: (event) => {
        this.event.set(event);
        this.loading.set(false);

        if (event.isSeated) {
          this.eventsApi.seatMap(slug).subscribe({
            next: (map) => this.seatMap.set(map),
            error: () => undefined
          });
        }

        this.eventsApi.search({ category: event.categorySlug, pageSize: 4 }).subscribe({
          next: (result) => this.related.set(result.items.filter((item) => item.id !== event.id).slice(0, 3)),
          error: () => undefined
        });
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  seatFill(seat: { status: SeatStatus; color: string }): string {
    if (seat.status === 'sold') return 'rgba(255,255,255,.18)';
    if (seat.status === 'held') return '#fbbf24';
    if (seat.status === 'blocked') return 'rgba(255,255,255,.08)';
    return seat.color;
  }

  share(): void {
    const event = this.event();
    if (!event) return;
    const url = window.location.href;
    if (navigator.share) {
      void navigator.share({ title: event.title, text: event.summary ?? undefined, url }).catch(() => undefined);
      return;
    }
    navigator.clipboard
      .writeText(url)
      .then(() => this.toast.success('Link copied', 'Share the event with a friend.'))
      .catch(() => this.toast.error('Could not copy link'));
  }
}
