import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EventDetail, MapPoint, Seat, SeatMap, SeatSection, SeatStats } from '../../core/models';
import { EventService } from '../../core/event.service';
import { BookingService } from '../../core/booking.service';
import { RealtimeService } from '../../core/realtime.service';
import { ToastService } from '../../core/toast.service';
import { apiError, dayAndTime, money, number, percent } from '../../core/format';
import { Icon } from '../../shared/icon';

type ViewBox = { x: number; y: number };

@Component({
  selector: 'app-seat-map-page',
  imports: [RouterLink, Icon, DecimalPipe],
  templateUrl: './seat-map-page.html',
  styleUrl: './seat-map-page.scss'
})
export class SeatMapPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly eventsApi = inject(EventService);
  private readonly bookingsApi = inject(BookingService);
  readonly realtime = inject(RealtimeService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly event = signal<EventDetail | null>(null);
  readonly map = signal<SeatMap | null>(null);
  readonly stats = signal<SeatStats | null>(null);
  readonly selectedIds = signal<ReadonlySet<string>>(new Set<string>());
  readonly hovered = signal<Seat | null>(null);
  readonly activeSection = signal<string | null>(null);
  readonly quantity = signal(1);
  readonly loading = signal(true);
  readonly mapLoading = signal(false);
  readonly holding = signal(false);
  readonly error = signal<string | null>(null);

  readonly scale = signal(1);
  readonly pan = signal<ViewBox>({ x: 0, y: 0 });
  readonly panning = signal(false);
  private moved = false;
  private drag = { x: 0, y: 0, panX: 0, panY: 0 };

  private joinedEventId: string | null = null;

  readonly selectedSeats = computed(() => {
    const map = this.map();
    const ids = this.selectedIds();
    if (!map) return [];
    return map.sections.flatMap((section) => section.seats).filter((seat) => ids.has(seat.id));
  });

  readonly subtotal = computed(() => this.selectedSeats().reduce((sum, seat) => sum + seat.price, 0));
  readonly serviceFee = computed(() => Math.round(this.subtotal() * 0.07 * 100) / 100);
  readonly total = computed(() => this.subtotal() + this.serviceFee());
  readonly selectedCount = computed(() => this.selectedSeats().length);
  readonly maxSeats = computed(() => this.map()?.maxSeatsPerOrder ?? 8);
  readonly canContinue = computed(() =>
    this.event()?.isSeated ? this.selectedSeats().length > 0 : this.quantity() >= 1
  );

  readonly gaSubtotal = computed(() => (this.event()?.basePrice ?? 0) * this.quantity());
  readonly gaFee = computed(() => Math.round(this.gaSubtotal() * 0.07 * 100) / 100);
  readonly gaTotal = computed(() => this.gaSubtotal() + this.gaFee());

  readonly occupancy = computed(() => {
    const stats = this.stats();
    return stats && stats.total ? percent(stats.sold + stats.held, stats.total) : 0;
  });

  readonly mapTransform = computed(() => {
    const map = this.map();
    if (!map) return '';
    const centerX = map.mapWidth / 2;
    const centerY = map.mapHeight / 2;
    const scale = this.scale();
    const pan = this.pan();
    return `translate(${pan.x} ${pan.y}) translate(${centerX} ${centerY}) scale(${scale}) translate(${-centerX} ${-centerY})`;
  });

  readonly money = money;
  readonly number = number;
  readonly dayAndTime = dayAndTime;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const slug = params.get('slug');
      if (slug) this.load(slug);
    });

    this.realtime.seatChanges$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ eventId, seats }) => this.applySeatChanges(eventId, seats));

    this.realtime.statsChanges$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ eventId, stats }) => {
        if (this.map()?.eventId === eventId) this.stats.set(stats);
      });

    this.destroyRef.onDestroy(() => {
      if (this.joinedEventId) void this.realtime.leaveEvent(this.joinedEventId);
    });
  }

  private load(slug: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.map.set(null);
    this.stats.set(null);
    this.selectedIds.set(new Set<string>());
    this.activeSection.set(null);
    this.quantity.set(1);
    this.resetView();

    this.eventsApi.get(slug).subscribe({
      next: (event) => {
        this.event.set(event);
        this.stats.set(event.stats);
        this.loading.set(false);

        if (this.joinedEventId && this.joinedEventId !== event.id) {
          void this.realtime.leaveEvent(this.joinedEventId);
          this.joinedEventId = null;
        }
        this.joinedEventId = event.id;
        void this.realtime.joinEvent(event.id);

        if (event.isSeated) this.loadSeatMap(slug);
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  private loadSeatMap(slug: string, keepSelection = false): void {
    this.mapLoading.set(true);
    this.eventsApi.seatMap(slug).subscribe({
      next: (map) => {
        this.map.set(map);
        this.stats.set(map.stats);
        this.mapLoading.set(false);
        if (keepSelection) {
          const available = new Set(
            map.sections.flatMap((section) =>
              section.seats.filter((seat) => seat.status === 'available').map((seat) => seat.id)
            )
          );
          this.selectedIds.update((ids) => new Set([...ids].filter((id) => available.has(id))));
        } else {
          this.selectedIds.set(new Set<string>());
        }
      },
      error: (error) => {
        this.toast.error('Seat map unavailable', apiError(error));
        this.mapLoading.set(false);
      }
    });
  }

  private refreshSeatMap(): void {
    const slug = this.event()?.slug;
    if (slug) this.loadSeatMap(slug, true);
  }

  private applySeatChanges(eventId: string, changes: { seatId: string; status: string }[]): void {
    const map = this.map();
    if (!map || map.eventId !== eventId) return;
    const changeMap = new Map(changes.map((change) => [change.seatId, change.status]));
    const nextStatuses: Record<string, Seat['status']> = {};

    const sections = map.sections.map((section) => ({
      ...section,
      seats: section.seats.map((seat) => {
        const status = changeMap.get(seat.id);
        if (!status) return seat;
        nextStatuses[seat.id] = status as Seat['status'];
        return { ...seat, status: status as Seat['status'] };
      })
    }));

    this.map.set({ ...map, sections });

    const grabbed = [...this.selectedIds()].filter((id) => {
      const status = nextStatuses[id];
      return status && status !== 'available';
    });

    if (grabbed.length) {
      this.selectedIds.update((ids) => new Set([...ids].filter((id) => !grabbed.includes(id))));
      this.toast.warning('Seats just changed', 'Someone else picked one of your seats. We kept the rest of your selection.');
    }
  }

  /* ---------- selection ---------- */

  isSelected(seat: Seat): boolean {
    return this.selectedIds().has(seat.id);
  }

  pickSeat(seat: Seat): void {
    if (this.moved) return;
    if (seat.status !== 'available') {
      this.toast.info('Seat unavailable', `${seat.code} is ${seat.status}. Try another seat nearby.`);
      return;
    }

    const ids = new Set(this.selectedIds());
    if (ids.has(seat.id)) {
      ids.delete(seat.id);
      this.selectedIds.set(ids);
      return;
    }

    if (ids.size >= this.maxSeats()) {
      this.toast.warning('Selection limit', `You can pick up to ${this.maxSeats()} seats per order.`);
      return;
    }

    ids.add(seat.id);
    this.selectedIds.set(ids);
  }

  removeSeat(seat: Seat): void {
    const ids = new Set(this.selectedIds());
    ids.delete(seat.id);
    this.selectedIds.set(ids);
  }

  clearSelection(): void {
    this.selectedIds.set(new Set<string>());
  }

  sectionAvailable(section: SeatSection): number {
    return section.seats.filter((seat) => seat.status === 'available').length;
  }

  sectionColor(code: string): string {
    return this.map()?.sections.find((section) => section.code === code)?.color ?? '#8f7dff';
  }

  sectionName(code: string): string {
    return this.map()?.sections.find((section) => section.code === code)?.name ?? code;
  }

  seatFill(seat: Seat): string {
    if (this.isSelected(seat)) return '#ffffff';
    if (seat.status === 'sold') return 'rgba(255,255,255,.16)';
    if (seat.status === 'held') return '#fbbf24';
    if (seat.status === 'blocked') return 'rgba(255,255,255,.07)';
    return this.sectionColor(seat.sectionCode);
  }

  isDimmed(seat: Seat): boolean {
    const active = this.activeSection();
    return !!active && seat.sectionCode !== active;
  }

  sectionPoints(section: SeatSection): string {
    return section.geometry.map((point: MapPoint) => `${point.x},${point.y}`).join(' ');
  }

  toggleSection(code: string): void {
    this.activeSection.set(this.activeSection() === code ? null : code);
  }

  /* ---------- pan & zoom ---------- */

  zoom(direction: number): void {
    this.scale.set(clamp(Number((this.scale() + direction * 0.25).toFixed(2)), 0.7, 3.5));
  }

  resetView(): void {
    this.scale.set(1);
    this.pan.set({ x: 0, y: 0 });
  }

  onWheel(event: WheelEvent): void {
    event.preventDefault();
    const factor = event.deltaY < 0 ? 1.12 : 0.89;
    this.scale.set(clamp(Number((this.scale() * factor).toFixed(3)), 0.7, 3.5));
  }

  startPan(event: PointerEvent): void {
    if (event.button !== 0) return;
    this.moved = false;
    this.panning.set(true);
    this.drag = { x: event.clientX, y: event.clientY, panX: this.pan().x, panY: this.pan().y };
  }

  movePan(event: PointerEvent): void {
    if (!this.panning()) return;
    const map = this.map();
    if (!map) return;
    const svg = event.currentTarget as SVGSVGElement;
    const rect = svg.getBoundingClientRect();
    const scaleX = map.mapWidth / Math.max(rect.width, 1);
    const scaleY = map.mapHeight / Math.max(rect.height, 1);
    const dx = (event.clientX - this.drag.x) * scaleX;
    const dy = (event.clientY - this.drag.y) * scaleY;
    if (Math.abs(event.clientX - this.drag.x) + Math.abs(event.clientY - this.drag.y) > 4) this.moved = true;
    this.pan.set({ x: this.drag.panX + dx, y: this.drag.panY + dy });
  }

  endPan(): void {
    this.panning.set(false);
  }

  /* ---------- checkout ---------- */

  continueToCheckout(): void {
    const event = this.event();
    if (!event || this.holding() || !this.canContinue()) return;
    this.holding.set(true);

    const payload = event.isSeated
      ? { eventId: event.id, seatIds: [...this.selectedIds()] }
      : { eventId: event.id, quantity: this.quantity() };

    this.bookingsApi.hold(payload).subscribe({
      next: (response) => {
        void this.router.navigate(['/checkout', response.bookingId]);
      },
      error: (error) => {
        this.holding.set(false);
        this.toast.error('Could not hold that selection', apiError(error));
        if (event.isSeated) this.refreshSeatMap();
      }
    });
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
