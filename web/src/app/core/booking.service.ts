import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminBooking,
  Attendee,
  Booking,
  BookingSummary,
  CheckInResult,
  HeldSeatsResponse,
  PagedResult
} from './models';

export interface HoldSeatsPayload {
  eventId: string;
  seatIds?: string[];
  quantity?: number;
  ticketType?: string;
}

export interface CheckoutPayload {
  paymentMethod: 'card' | 'demo';
  cardNumber?: string;
  cardHolder?: string;
  expiry?: string;
  cvv?: string;
}

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  hold(payload: HoldSeatsPayload): Observable<HeldSeatsResponse> {
    return this.http.post<HeldSeatsResponse>(`${this.api}/api/bookings/hold`, payload);
  }

  checkout(bookingId: string, payload: CheckoutPayload): Observable<Booking> {
    return this.http.post<Booking>(`${this.api}/api/bookings/${bookingId}/checkout`, payload);
  }

  release(bookingId: string): Observable<Booking> {
    return this.http.post<Booking>(`${this.api}/api/bookings/${bookingId}/release`, {});
  }

  cancel(bookingId: string, reason?: string): Observable<Booking> {
    return this.http.post<Booking>(`${this.api}/api/bookings/${bookingId}/cancel`, { reason });
  }

  mine(page = 1, pageSize = 20): Observable<PagedResult<BookingSummary>> {
    return this.http.get<PagedResult<BookingSummary>>(`${this.api}/api/bookings`, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize)
    });
  }

  get(id: string): Observable<Booking> {
    return this.http.get<Booking>(`${this.api}/api/bookings/${id}`);
  }

  adminAll(page = 1, pageSize = 20, q?: string, status?: string): Observable<PagedResult<AdminBooking>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q) params = params.set('q', q);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<AdminBooking>>(`${this.api}/api/bookings/admin/all`, { params });
  }

  checkIn(ticketCode: string): Observable<CheckInResult> {
    return this.http.post<CheckInResult>(`${this.api}/api/tickets/check-in`, { ticketCode });
  }

  attendees(eventId: string, page = 1, pageSize = 100, q?: string): Observable<PagedResult<Attendee>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q) params = params.set('q', q);
    return this.http.get<PagedResult<Attendee>>(`${this.api}/api/tickets/event/${eventId}`, { params });
  }

  exportAttendees(eventId: string): Observable<Blob> {
    return this.http.get(`${this.api}/api/tickets/event/${eventId}/export`, { responseType: 'blob' });
  }
}
