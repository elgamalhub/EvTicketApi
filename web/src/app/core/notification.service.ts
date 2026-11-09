import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AppNotification, PagedResult } from './models';
import { RealtimeService } from './realtime.service';
import { ToastService } from './toast.service';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;
  private readonly realtime = inject(RealtimeService);
  private readonly toast = inject(ToastService);

  private readonly _items = signal<AppNotification[]>([]);
  private readonly _unread = signal(0);
  readonly items = this._items.asReadonly();
  readonly unread = this._unread.asReadonly();

  constructor() {
    this.realtime.notifications$.subscribe((notification) => {
      this._items.update((items) => [notification, ...items.filter((item) => item.id !== notification.id)].slice(0, 50));
      this._unread.update((count) => count + 1);
      this.toast.info(notification.title, notification.body);
    });
  }

  load(page = 1, pageSize = 20, unreadOnly = false): Observable<PagedResult<AppNotification>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('unreadOnly', unreadOnly);
    return this.http
      .get<PagedResult<AppNotification>>(`${this.api}/api/notifications`, { params })
      .pipe(tap((result) => this._items.set(result.items)));
  }

  refreshUnread(): Observable<{ count: number }> {
    return this.http
      .get<{ count: number }>(`${this.api}/api/notifications/unread-count`)
      .pipe(tap((result) => this._unread.set(result.count)));
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/api/notifications/${id}/read`, {}).pipe(
      tap(() => {
        this._items.update((items) => items.map((item) => (item.id === id ? { ...item, isRead: true } : item)));
        this._unread.update((count) => Math.max(0, count - 1));
      })
    );
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.api}/api/notifications/read-all`, {}).pipe(
      tap(() => {
        this._items.update((items) => items.map((item) => ({ ...item, isRead: true })));
        this._unread.set(0);
      })
    );
  }

  reset(): void {
    this._items.set([]);
    this._unread.set(0);
  }
}
