import { Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BookingService } from '../../core/booking.service';
import { EventService } from '../../core/event.service';
import { ToastService } from '../../core/toast.service';
import { Attendee, CheckInResult, EventSummary } from '../../core/models';
import { apiError, dayAndTime, number, seatLabel, timeOfDay } from '../../core/format';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-check-in-page',
  imports: [FormsModule, RouterLink, Icon],
  templateUrl: './check-in-page.html',
  styleUrl: './check-in-page.scss'
})
export class CheckInPage {
  private readonly eventsApi = inject(EventService);
  private readonly bookingsApi = inject(BookingService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly events = signal<EventSummary[]>([]);
  readonly selectedId = signal('');
  readonly loadingEvents = signal(true);
  readonly eventsError = signal<string | null>(null);

  readonly attendees = signal<Attendee[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly totalPages = signal(1);
  readonly loadingAttendees = signal(false);
  readonly attendeesError = signal<string | null>(null);
  readonly query = signal('');
  readonly checkedInTotal = signal(0);

  readonly code = signal('');
  readonly checkingIn = signal(false);
  readonly result = signal<CheckInResult | null>(null);
  readonly exporting = signal(false);

  readonly codeInput = viewChild<ElementRef<HTMLInputElement>>('codeInput');

  readonly selectedEvent = computed(() => this.events().find((event) => event.id === this.selectedId()) ?? null);
  readonly checkedInOnPage = computed(() => this.attendees().filter((attendee) => attendee.isCheckedIn).length);
  readonly remaining = computed(() => Math.max(this.total() - this.checkedInTotal(), 0));
  readonly progress = computed(() => {
    const total = this.total();
    return total === 0 ? 0 : Math.round((this.checkedInTotal() / total) * 100);
  });

  readonly number = number;
  readonly dayAndTime = dayAndTime;
  readonly timeOfDay = timeOfDay;
  readonly seatLabel = seatLabel;

  private searchTimer?: ReturnType<typeof setTimeout>;
  private statsToken = 0;

  constructor() {
    const preselect = this.route.snapshot.queryParamMap.get('event');
    this.loadEvents(preselect);

    this.destroyRef.onDestroy(() => {
      if (this.searchTimer) clearTimeout(this.searchTimer);
    });
  }

  loadEvents(preselect?: string | null): void {
    this.loadingEvents.set(true);
    this.eventsError.set(null);

    this.eventsApi.organizerEvents(1, 100).subscribe({
      next: (result) => {
        this.events.set(result.items);
        this.loadingEvents.set(false);
        const items = result.items;
        const chosen =
          preselect && items.some((event) => event.id === preselect)
            ? preselect
            : items[0]?.id ?? '';
        if (chosen) {
          this.selectEvent(chosen, true);
        }
      },
      error: (error) => {
        this.eventsError.set(apiError(error));
        this.loadingEvents.set(false);
      }
    });
  }

  selectEvent(id: string, initial = false): void {
    if (!initial && id === this.selectedId()) return;
    this.selectedId.set(id);
    this.page.set(1);
    this.query.set('');
    this.result.set(null);
    this.attendees.set([]);
    this.checkedInTotal.set(0);
    this.loadAttendees(1);
    this.loadStats();
    setTimeout(() => this.codeInput()?.nativeElement.focus(), 60);
  }

  loadAttendees(page = this.page()): void {
    const eventId = this.selectedId();
    if (!eventId) return;

    this.loadingAttendees.set(true);
    this.attendeesError.set(null);

    this.bookingsApi.attendees(eventId, page, 100, this.query().trim() || undefined).subscribe({
      next: (result) => {
        if (this.selectedId() !== eventId) return;
        this.attendees.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.loadingAttendees.set(false);
      },
      error: (error) => {
        this.attendeesError.set(apiError(error));
        this.loadingAttendees.set(false);
      }
    });
  }

  /** Counts check-ins across every page so the progress bar is accurate for large events. */
  private loadStats(): void {
    const eventId = this.selectedId();
    const token = ++this.statsToken;
    if (!eventId) return;

    const collect = (page: number, checked: number): void => {
      this.bookingsApi.attendees(eventId, page, 200).subscribe({
        next: (result) => {
          if (token !== this.statsToken || this.selectedId() !== eventId) return;
          const running = checked + result.items.filter((attendee) => attendee.isCheckedIn).length;
          if (result.hasNext) {
            collect(page + 1, running);
          } else {
            this.checkedInTotal.set(running);
          }
        },
        error: () => undefined
      });
    };

    collect(1, 0);
  }

  onSearch(value: string): void {
    this.query.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.page.set(1);
      this.loadAttendees(1);
    }, 350);
  }

  clearSearch(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.query.set('');
    this.page.set(1);
    this.loadAttendees(1);
  }

  prevPage(): void {
    if (this.page() <= 1) return;
    this.loadAttendees(this.page() - 1);
  }

  nextPage(): void {
    if (this.page() >= this.totalPages()) return;
    this.loadAttendees(this.page() + 1);
  }

  submit(): void {
    const ticketCode = this.code().trim();
    if (!ticketCode || this.checkingIn()) return;

    this.checkingIn.set(true);
    this.result.set(null);

    this.bookingsApi.checkIn(ticketCode).subscribe({
      next: (result) => {
        this.checkingIn.set(false);
        this.result.set(result);
        this.code.set('');

        if (result.success) {
          this.toast.success('Guest checked in', `${result.attendeeName ?? 'Guest'} · ${result.seat ?? 'General admission'}`);
          this.loadAttendees(this.page());
          this.loadStats();
        } else {
          this.toast.error('Check-in rejected', result.message);
        }

        setTimeout(() => this.codeInput()?.nativeElement.focus(), 40);
      },
      error: (error) => {
        this.checkingIn.set(false);
        this.code.set('');
        this.result.set({ success: false, message: apiError(error) });
        this.toast.error('Check-in failed', apiError(error));
        setTimeout(() => this.codeInput()?.nativeElement.focus(), 40);
      }
    });
  }

  exportCsv(): void {
    const event = this.selectedEvent();
    if (!event || this.exporting()) return;

    this.exporting.set(true);
    this.bookingsApi.exportAttendees(event.id).subscribe({
      next: (blob) => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${event.slug}-attendees.csv`;
        link.click();
        URL.revokeObjectURL(url);
        this.toast.success('Export ready', `${number(this.total())} tickets exported to CSV.`);
      },
      error: (error) => {
        this.exporting.set(false);
        this.toast.error('Could not export attendees', apiError(error));
      }
    });
  }

  trackAttendee(_: number, attendee: Attendee): string {
    return attendee.ticketCode;
  }
}
