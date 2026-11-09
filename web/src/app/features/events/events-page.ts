import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { EventCategory, EventFilters, EventSummary, PagedResult, Venue } from '../../core/models';
import { EventService } from '../../core/event.service';
import { apiError } from '../../core/format';
import { EventCard } from '../../shared/event-card';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-events-page',
  imports: [EventCard, Icon],
  templateUrl: './events-page.html',
  styleUrl: './events-page.scss'
})
export class EventsPage {
  private readonly eventsApi = inject(EventService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly result = signal<PagedResult<EventSummary> | null>(null);
  readonly categories = signal<EventCategory[]>([]);
  readonly venues = signal<Venue[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly filters = signal<EventFilters>({ page: 1, pageSize: 12, sort: 'date' });
  readonly searchText = signal('');
  readonly priceText = signal('');
  readonly filtersOpen = signal(false);

  readonly cities = computed(() => Array.from(new Set(this.venues().map((venue) => venue.city))).sort());
  readonly categoryName = computed(() => {
    const slug = this.filters().category;
    return slug ? this.categories().find((category) => category.slug === slug)?.name ?? slug : null;
  });
  readonly sortLabel = computed(() => {
    const sort = this.filters().sort ?? 'date';
    return { date: 'Date', price_asc: 'Price: low to high', price_desc: 'Price: high to low', popular: 'Most popular', newest: 'Newest' }[sort] ?? 'Date';
  });
  readonly activeCount = computed(() => {
    const f = this.filters();
    return [f.q, f.category, f.city, f.minPrice, f.maxPrice, f.includePast].filter(Boolean).length;
  });
  readonly pages = computed(() => {
    const result = this.result();
    if (!result) return [];
    const total = result.totalPages ?? Math.ceil(result.total / result.pageSize);
    const current = result.page;
    const start = Math.max(1, Math.min(current - 2, total - 4));
    return Array.from({ length: Math.min(5, total) }, (_, index) => start + index).filter((page) => page >= 1 && page <= total);
  });

  constructor() {
    this.route.queryParamMap.subscribe((params) => {
      const minPrice = params.get('minPrice');
      const maxPrice = params.get('maxPrice');
      const filters: EventFilters = {
        q: params.get('q') ?? undefined,
        category: params.get('category') ?? undefined,
        city: params.get('city') ?? undefined,
        sort: params.get('sort') ?? 'date',
        page: Number(params.get('page') ?? 1) || 1,
        pageSize: 12,
        minPrice: minPrice ? Number(minPrice) : undefined,
        maxPrice: maxPrice ? Number(maxPrice) : undefined,
        includePast: params.get('includePast') === 'true'
      };
      this.filters.set(filters);
      this.searchText.set(filters.q ?? '');
      this.priceText.set(minPrice && maxPrice ? `${minPrice}-${maxPrice}` : '');
      this.load(filters);
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

  private load(filters: EventFilters): void {
    this.loading.set(true);
    this.error.set(null);
    this.eventsApi.search(filters).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
        window.scrollTo({ top: 0, behavior: 'smooth' });
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  apply(patch: Partial<EventFilters>, resetPage = true): void {
    const current = this.filters();
    const next: EventFilters = { ...current, ...patch };
    if (resetPage && patch.page === undefined) next.page = 1;

    void this.router.navigate(['/events'], {
      queryParams: {
        q: next.q || null,
        category: next.category || null,
        city: next.city || null,
        sort: next.sort && next.sort !== 'date' ? next.sort : null,
        page: next.page && next.page > 1 ? next.page : null,
        minPrice: next.minPrice ?? null,
        maxPrice: next.maxPrice ?? null,
        includePast: next.includePast ? 'true' : null
      }
    });
  }

  submitSearch(): void {
    this.apply({ q: this.searchText().trim() || undefined });
  }

  applyPrice(): void {
    const [min, max] = this.priceText()
      .split(/[-–]/)
      .map((value) => Number(value.trim()))
      .filter((value) => Number.isFinite(value) && value >= 0);
    this.apply({ minPrice: min, maxPrice: max });
  }

  toggleCategory(slug: string): void {
    this.apply({ category: this.filters().category === slug ? undefined : slug });
  }

  clearAll(): void {
    this.searchText.set('');
    this.priceText.set('');
    void this.router.navigate(['/events']);
  }

  goToPage(page: number): void {
    this.apply({ page }, false);
  }
}
