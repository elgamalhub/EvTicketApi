import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EventService } from '../../core/event.service';
import { ToastService } from '../../core/toast.service';
import { EventCategory, EventDetail, Venue } from '../../core/models';
import { apiError, dateLong, money, number, timeOfDay } from '../../core/format';
import { Icon } from '../../shared/icon';

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function toLocalInput(iso: string | null | undefined): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function defaultStart(): string {
  const date = new Date();
  date.setDate(date.getDate() + 14);
  date.setHours(20, 0, 0, 0);
  return toLocalInput(date.toISOString());
}

function toIso(local: string): string | undefined {
  if (!local) return undefined;
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

@Component({
  selector: 'app-event-form-page',
  imports: [FormsModule, RouterLink, Icon],
  templateUrl: './event-form-page.html',
  styleUrl: './event-form-page.scss'
})
export class EventFormPage {
  private readonly eventsApi = inject(EventService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly eventId = signal<string | null>(null);
  readonly existing = signal<EventDetail | null>(null);
  readonly categories = signal<EventCategory[]>([]);
  readonly venues = signal<Venue[]>([]);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly title = signal('');
  readonly summary = signal('');
  readonly description = signal('');
  readonly categoryId = signal('');
  readonly venueId = signal('');
  readonly startsAt = signal(defaultStart());
  readonly doorsOpenAt = signal('');
  readonly endsAt = signal('');
  readonly basePrice = signal('45');
  readonly isSeated = signal(true);
  readonly bannerUrl = signal('');
  readonly tags = signal('');
  readonly ageRating = signal('All ages');
  readonly publish = signal(true);

  readonly isEdit = computed(() => !!this.eventId());
  readonly selectedVenue = computed(() => this.venues().find((venue) => venue.id === this.venueId()) ?? null);
  readonly selectedCategory = computed(
    () => this.categories().find((category) => category.id === this.categoryId()) ?? null
  );

  readonly dateLong = dateLong;
  readonly timeOfDay = timeOfDay;
  readonly money = money;
  readonly number = number;

  readonly rank = computed(() => {
    const price = Number(this.basePrice()) || 0;
    const venue = this.selectedVenue();
    const multiplier = venue ? Math.max(...venue.sections.map((section) => section.priceMultiplier), 1) : 1;
    return { low: price, high: price * multiplier };
  });

  readonly previewTitle = computed(() => this.title().trim() || 'Your event title');
  readonly previewDate = computed(() => {
    const iso = toIso(this.startsAt());
    return iso ? `${dateLong(iso)} · ${timeOfDay(iso)}` : 'Choose a date and time';
  });

  readonly timeValid = computed(() => {
    const start = toIso(this.startsAt());
    if (!start) return false;
    const startMs = new Date(start).getTime();
    const doors = toIso(this.doorsOpenAt());
    const end = toIso(this.endsAt());
    if (doors && new Date(doors).getTime() >= startMs) return false;
    if (end && new Date(end).getTime() <= startMs) return false;
    return true;
  });

  readonly bannerValid = computed(() => {
    const banner = this.bannerUrl().trim();
    return !banner || /^https?:\/\/[^\s]+$/i.test(banner);
  });

  readonly canSubmit = computed(
    () =>
      this.title().trim().length >= 3 &&
      !!this.categoryId() &&
      (this.isEdit() || !!this.venueId()) &&
      this.timeValid() &&
      this.bannerValid() &&
      Number(this.basePrice()) >= 0 &&
      !this.saving()
  );

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.eventId.set(id);

    this.eventsApi.categories().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        this.matchCategory();
      },
      error: () => undefined
    });

    this.eventsApi.venues().subscribe({
      next: (venues) => {
        this.venues.set(venues);
        this.matchVenue();
        if (!this.isEdit() && !this.venueId() && venues.length > 0) {
          this.venueId.set(venues[0].id);
        }
      },
      error: () => undefined
    });

    if (id) {
      this.loadExisting(id);
    } else {
      this.loading.set(false);
    }
  }

  private matchCategory(): void {
    const event = this.existing();
    if (!event || this.categoryId()) return;
    const match = this.categories().find((category) => category.slug === event.categorySlug);
    if (match) this.categoryId.set(match.id);
  }

  private matchVenue(): void {
    const event = this.existing();
    if (!event || this.venueId()) return;
    const match = this.venues().find((venue) => venue.slug === event.venueSlug);
    if (match) this.venueId.set(match.id);
  }

  private loadExisting(id: string): void {
    this.loading.set(true);
    this.eventsApi.get(id).subscribe({
      next: (event) => {
        this.existing.set(event);
        this.title.set(event.title);
        this.summary.set(event.summary ?? '');
        this.description.set(event.description ?? '');
        this.startsAt.set(toLocalInput(event.startsAt));
        this.doorsOpenAt.set(toLocalInput(event.doorsOpenAt));
        this.endsAt.set(toLocalInput(event.endsAt));
        this.basePrice.set(String(event.basePrice));
        this.isSeated.set(event.isSeated);
        this.bannerUrl.set(event.bannerUrl ?? '');
        this.tags.set(event.tags.join(', '));
        this.ageRating.set(event.ageRating || 'All ages');
        this.publish.set(event.status.toLowerCase() !== 'draft');

        this.matchCategory();
        this.matchVenue();
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  onSubmit(): void {
    if (!this.canSubmit()) {
      this.error.set('Check the highlighted fields before saving.');
      return;
    }

    const payload = {
      title: this.title().trim(),
      summary: this.summary().trim() || undefined,
      description: this.description().trim() || undefined,
      categoryId: this.categoryId(),
      venueId: this.venueId(),
      startsAt: toIso(this.startsAt())!,
      endsAt: toIso(this.endsAt()) ?? undefined,
      doorsOpenAt: toIso(this.doorsOpenAt()) ?? undefined,
      basePrice: Number(this.basePrice()),
      isSeated: this.isSeated(),
      bannerUrl: this.bannerUrl().trim() || undefined,
      tags: this.tags().trim() || undefined,
      ageRating: this.ageRating(),
      publish: this.publish()
    };

    this.saving.set(true);
    this.error.set(null);

    const request = this.eventId()
      ? this.eventsApi.update(this.eventId()!, { ...payload, status: this.publish() ? 'Published' : 'Draft' })
      : this.eventsApi.create(payload);

    request.subscribe({
      next: (event) => {
        this.saving.set(false);
        this.toast.success(
          this.isEdit() ? 'Event updated' : this.publish() ? 'Event published' : 'Draft saved',
          this.publish() ? `${event.title} is live for ${event.city}.` : `${event.title} is saved as a draft.`
        );
        void this.router.navigate(['/organizer']);
      },
      error: (error) => {
        this.saving.set(false);
        this.error.set(apiError(error));
        this.toast.error('Could not save the event', apiError(error));
      }
    });
  }

  toggleSeated(): void {
    this.isSeated.set(!this.isSeated());
  }
}
