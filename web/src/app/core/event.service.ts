import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CreateEventPayload,
  EventCategory,
  EventDetail,
  EventFilters,
  EventSummary,
  PagedResult,
  SeatMap,
  Venue
} from './models';

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  search(filters: EventFilters = {}): Observable<PagedResult<EventSummary>> {
    let params = new HttpParams();
    const entries: [string, unknown][] = [
      ['q', filters.q],
      ['category', filters.category],
      ['city', filters.city],
      ['sort', filters.sort],
      ['page', filters.page],
      ['pageSize', filters.pageSize],
      ['minPrice', filters.minPrice],
      ['maxPrice', filters.maxPrice],
      ['includePast', filters.includePast]
    ];
    for (const [key, value] of entries) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<EventSummary>>(`${this.api}/api/events`, { params });
  }

  featured(take = 6): Observable<EventSummary[]> {
    return this.http.get<EventSummary[]>(`${this.api}/api/events/featured?take=${take}`);
  }

  get(slug: string): Observable<EventDetail> {
    return this.http.get<EventDetail>(`${this.api}/api/events/${encodeURIComponent(slug)}`);
  }

  seatMap(slug: string): Observable<SeatMap> {
    return this.http.get<SeatMap>(`${this.api}/api/events/${encodeURIComponent(slug)}/seat-map`);
  }

  categories(): Observable<EventCategory[]> {
    return this.http.get<EventCategory[]>(`${this.api}/api/catalog/categories`);
  }

  venues(): Observable<Venue[]> {
    return this.http.get<Venue[]>(`${this.api}/api/catalog/venues`);
  }

  organizerEvents(page = 1, pageSize = 50, status?: string): Observable<PagedResult<EventSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<EventSummary>>(`${this.api}/api/events/organizer/mine`, { params });
  }

  adminEvents(page = 1, pageSize = 50, q?: string, status?: string): Observable<PagedResult<EventSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q) params = params.set('q', q);
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<EventSummary>>(`${this.api}/api/events/admin/all`, { params });
  }

  create(payload: CreateEventPayload): Observable<EventDetail> {
    return this.http.post<EventDetail>(`${this.api}/api/events`, payload);
  }

  update(id: string, payload: CreateEventPayload & { status?: string }): Observable<EventDetail> {
    return this.http.put<EventDetail>(`${this.api}/api/events/${id}`, payload);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/api/events/${id}`);
  }
}
