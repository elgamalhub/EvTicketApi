import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Booking } from '../../core/models';
import { BookingService } from '../../core/booking.service';
import { ToastService } from '../../core/toast.service';
import { apiError, countdownParts, dateLong, dayAndTime, dayMonth, money, seatLabel, timeOfDay } from '../../core/format';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-checkout-page',
  imports: [FormsModule, RouterLink, Icon],
  templateUrl: './checkout-page.html',
  styleUrl: './checkout-page.scss'
})
export class CheckoutPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly bookingsApi = inject(BookingService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly booking = signal<Booking | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly paying = signal(false);
  readonly releasing = signal(false);
  readonly now = signal(Date.now());
  readonly completed = signal(false);

  readonly method = signal<'card' | 'demo'>('card');
  readonly cardNumber = signal('');
  readonly cardHolder = signal('');
  readonly expiry = signal('');
  readonly cvv = signal('');
  readonly touched = signal(false);

  private timer?: ReturnType<typeof setInterval>;
  private holdReleased = false;

  readonly countdown = computed(() =>
    countdownParts(this.booking()?.expiresAt ?? new Date(this.now()), this.now())
  );
  readonly expired = computed(() => this.countdown().expired);
  readonly isPending = computed(() => this.booking()?.status === 'pending');
  readonly canPay = computed(
    () => !!this.booking() && this.isPending() && !this.expired() && !this.paying() && !this.releasing()
  );

  readonly cardDigits = computed(() => this.cardNumber().replace(/\D/g, ''));
  readonly cardBrand = computed(() => {
    const digits = this.cardDigits();
    if (digits.startsWith('4')) return 'Visa';
    if (/^5[1-5]/.test(digits) || /^2[2-7]/.test(digits)) return 'Mastercard';
    if (/^3[47]/.test(digits)) return 'Amex';
    if (digits.startsWith('6')) return 'Discover';
    return 'Card';
  });
  readonly cardMask = computed(() => {
    const digits = this.cardDigits();
    if (!digits) return '•••• •••• •••• ••••';
    const padded = digits.padEnd(16, '•').slice(0, 16);
    return padded.replace(/(.{4})/g, '$1 ').trim();
  });

  readonly cardProblem = computed(() => {
    if (this.method() === 'demo') return null;
    const digits = this.cardDigits();
    if (!digits) return 'Enter your card number.';
    if (digits.length < 13 || digits.length > 19) return 'Card number looks incomplete.';
    if (!luhn(digits)) return 'That card number is not valid.';
    if (!this.cardHolder().trim()) return 'Enter the name on the card.';
    if (!/^\d{2}\s?\/\s?\d{2}$/.test(this.expiry().trim())) return 'Expiry must be MM/YY.';
    if (isExpired(this.expiry())) return 'That card has expired.';
    if (!/^\d{3,4}$/.test(this.cvv().trim())) return 'CVV must be 3 or 4 digits.';
    return null;
  });

  readonly cardError = computed(() => (this.touched() ? this.cardProblem() : null));

  readonly readyToPay = computed(() => this.method() === 'demo' || this.cardProblem() === null);

  readonly tickets = computed(() => this.booking()?.tickets ?? []);
  readonly money = money;
  readonly dateLong = dateLong;
  readonly dayAndTime = dayAndTime;
  readonly dayMonth = dayMonth;
  readonly timeOfDay = timeOfDay;
  readonly seatLabel = seatLabel;

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('bookingId');
      if (id) this.load(id);
    });

    this.timer = setInterval(() => this.now.set(Date.now()), 1000);

    this.destroyRef.onDestroy(() => {
      if (this.timer) clearInterval(this.timer);
      const booking = this.booking();
      if (booking && booking.status === 'pending' && !this.completed() && !this.holdReleased) {
        this.bookingsApi.release(booking.id).subscribe({ error: () => undefined });
      }
    });
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.bookingsApi.get(id).subscribe({
      next: (booking) => {
        this.booking.set(booking);
        this.loading.set(false);
        if (booking.status === 'confirmed') {
          this.completed.set(true);
          void this.router.navigate(['/bookings', booking.id], { replaceUrl: true });
        }
      },
      error: (error) => {
        this.error.set(apiError(error));
        this.loading.set(false);
      }
    });
  }

  setMethod(method: 'card' | 'demo'): void {
    this.method.set(method);
    this.touched.set(false);
  }

  onCardInput(value: string): void {
    const digits = value.replace(/\D/g, '').slice(0, 19);
    this.cardNumber.set(digits.replace(/(.{4})/g, '$1 ').trim());
  }

  onExpiryInput(value: string): void {
    const digits = value.replace(/\D/g, '').slice(0, 4);
    this.expiry.set(digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits);
  }

  onCvvInput(value: string): void {
    this.cvv.set(value.replace(/\D/g, '').slice(0, 4));
  }

  fillDemoCard(): void {
    this.setMethod('card');
    this.cardNumber.set('4242 4242 4242 4242');
    this.cardHolder.set('Alex Morgan');
    this.expiry.set('12/29');
    this.cvv.set('123');
    this.touched.set(false);
    this.toast.info('Demo card filled', 'Use any valid future expiry and a 3-digit CVV.');
  }

  pay(): void {
    const booking = this.booking();
    if (!booking || !this.canPay()) return;

    if (this.method() === 'card') {
      this.touched.set(true);
      if (this.cardError()) {
        this.toast.error('Check your card details', this.cardError() ?? undefined);
        return;
      }
    }

    this.paying.set(true);
    const payload =
      this.method() === 'demo'
        ? { paymentMethod: 'demo' as const }
        : {
            paymentMethod: 'card' as const,
            cardNumber: this.cardDigits(),
            cardHolder: this.cardHolder().trim(),
            expiry: this.expiry().trim(),
            cvv: this.cvv().trim()
          };

    this.bookingsApi.checkout(booking.id, payload).subscribe({
      next: (confirmed) => {
        this.completed.set(true);
        this.paying.set(false);
        this.toast.success('Payment approved', `Booking ${confirmed.reference} is confirmed — your tickets are ready.`);
        void this.router.navigate(['/bookings', confirmed.id]);
      },
      error: (error) => {
        this.paying.set(false);
        this.toast.error('Payment could not be completed', apiError(error));
        if (error?.status === 409) this.load(booking.id);
      }
    });
  }

  releaseAndLeave(): void {
    const booking = this.booking();
    if (!booking || this.releasing()) return;
    this.releasing.set(true);
    this.bookingsApi.release(booking.id).subscribe({
      next: () => {
        this.holdReleased = true;
        this.toast.info('Seats released', 'Your hold has been released back to the venue.');
        void this.router.navigate(['/events', booking.eventSlug, 'seats']);
      },
      error: (error) => {
        this.releasing.set(false);
        this.toast.error('Could not release the hold', apiError(error));
      }
    });
  }
}

function luhn(digits: string): boolean {
  let sum = 0;
  let double = false;
  for (let index = digits.length - 1; index >= 0; index--) {
    let value = Number(digits[index]);
    if (double) {
      value *= 2;
      if (value > 9) value -= 9;
    }
    sum += value;
    double = !double;
  }
  return sum % 10 === 0;
}

function isExpired(expiry: string): boolean {
  const [monthText, yearText] = expiry.replace(/\s/g, '').split('/');
  const month = Number(monthText);
  const year = Number(yearText);
  if (!month || !year || month < 1 || month > 12) return true;
  const end = new Date(2000 + year, month, 1).getTime();
  return end <= Date.now();
}
