import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Booking } from '../../core/models';
import { BookingService } from '../../core/booking.service';
import { ToastService } from '../../core/toast.service';
import { apiError, dateLong, dayAndTime, money, seatLabel, statusLabel, timeOfDay } from '../../core/format';
import { Icon } from '../../shared/icon';
import { QrCode } from '../../shared/qr-code';

@Component({
  selector: 'app-booking-detail-page',
  imports: [FormsModule, RouterLink, Icon, QrCode],
  templateUrl: './booking-detail-page.html',
  styleUrl: './booking-detail-page.scss'
})
export class BookingDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly bookingsApi = inject(BookingService);
  private readonly toast = inject(ToastService);

  readonly booking = signal<Booking | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly cancelling = signal(false);
  readonly cancelOpen = signal(false);
  readonly reason = signal('');
  readonly copied = signal<string | null>(null);

  readonly isConfirmed = computed(() => this.booking()?.status === 'confirmed');
  readonly isPending = computed(() => this.booking()?.status === 'pending');
  readonly isCancelled = computed(() => {
    const status = this.booking()?.status;
    return status === 'cancelled' || status === 'expired';
  });
  readonly checkedInCount = computed(() => this.booking()?.tickets.filter((ticket) => ticket.isCheckedIn).length ?? 0);

  readonly money = money;
  readonly dateLong = dateLong;
  readonly dayAndTime = dayAndTime;
  readonly timeOfDay = timeOfDay;
  readonly seatLabel = seatLabel;
  readonly statusLabel = statusLabel;

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.load(id);
    });
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.bookingsApi.get(id).subscribe({
      next: (booking) => {
        this.booking.set(booking);
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  cancel(): void {
    const booking = this.booking();
    if (!booking || this.cancelling()) return;
    this.cancelling.set(true);
    this.bookingsApi.cancel(booking.id, this.reason().trim() || undefined).subscribe({
      next: (updated) => {
        this.booking.set(updated);
        this.cancelling.set(false);
        this.cancelOpen.set(false);
        this.toast.success('Booking cancelled', 'Your payment has been refunded and the seats are back on sale.');
      },
      error: (error) => {
        this.cancelling.set(false);
        this.toast.error('Could not cancel this booking', apiError(error));
      }
    });
  }

  copy(code: string): void {
    navigator.clipboard
      .writeText(code)
      .then(() => {
        this.copied.set(code);
        setTimeout(() => this.copied.set(null), 2200);
        this.toast.success('Ticket code copied', 'Paste it into the check-in desk if scanning is unavailable.');
      })
      .catch(() => this.toast.error('Could not copy', 'Your browser blocked clipboard access.'));
  }

  print(): void {
    window.print();
  }

  addToCalendar(): void {
    const booking = this.booking();
    if (!booking) return;

    const start = new Date(booking.eventStartsAt);
    const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
    const stamp = (date: Date) => date.toISOString().replace(/[-:]/g, '').split('.')[0] + 'Z';
    const ics = [
      'BEGIN:VCALENDAR',
      'VERSION:2.0',
      'PRODID:-//EvTicket//Booking//EN',
      'BEGIN:VEVENT',
      `UID:${booking.id}@evticket`,
      `DTSTAMP:${stamp(new Date())}`,
      `DTSTART:${stamp(start)}`,
      `DTEND:${stamp(end)}`,
      `SUMMARY:${booking.eventTitle}`,
      `LOCATION:${booking.venueName}, ${booking.city}`,
      `DESCRIPTION:EvTicket booking ${booking.reference}. ${booking.itemCount} ticket(s).`,
      'END:VEVENT',
      'END:VCALENDAR'
    ].join('\r\n');

    const blob = new Blob([ics], { type: 'text/calendar;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `evticket-${booking.reference}.ics`;
    link.click();
    URL.revokeObjectURL(url);
    this.toast.success('Calendar file downloaded', 'Open it to add the event to your calendar.');
  }
}
