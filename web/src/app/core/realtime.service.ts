import { inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AppNotification, SeatChange, SeatStats } from './models';
import { AuthService } from './auth.service';

export type RealtimeStatus = 'offline' | 'connecting' | 'live';

/**
 * One SignalR connection shared by the whole app. Seats are broadcast per event
 * group; notifications are delivered to a per-user group.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private hub?: HubConnection;
  private starting?: Promise<HubConnection>;
  private handlersBound = false;
  private readonly eventRefs = new Map<string, number>();

  readonly status = signal<RealtimeStatus>('offline');
  readonly seatChanges$ = new Subject<{ eventId: string; seats: SeatChange[] }>();
  readonly statsChanges$ = new Subject<{ eventId: string; stats: SeatStats }>();
  readonly notifications$ = new Subject<AppNotification>();

  async connect(): Promise<HubConnection | null> {
    if (this.hub?.state === HubConnectionState.Connected) return this.hub;
    if (this.starting) return this.starting;

    if (!this.hub) {
      this.hub = new HubConnectionBuilder()
        .withUrl(`${environment.apiUrl}/hubs/seats`, {
          accessTokenFactory: () => this.auth.accessToken ?? '',
          withCredentials: false
        })
        .withAutomaticReconnect([0, 1500, 4000, 8000, 15000])
        .configureLogging(LogLevel.Warning)
        .build();

      this.bindHandlers(this.hub);
    }

    const hub = this.hub;
    this.status.set('connecting');

    this.starting = hub
      .start()
      .then(async () => {
        this.status.set('live');
        if (this.auth.isAuthenticated()) {
          await hub.invoke('JoinMyNotifications').catch(() => undefined);
        }
        for (const eventId of this.eventRefs.keys()) {
          await hub.invoke('JoinEvent', eventId).catch(() => undefined);
        }
        return hub;
      })
      .catch(() => {
        this.status.set('offline');
        return hub;
      })
      .finally(() => {
        this.starting = undefined;
      });

    return this.starting;
  }

  async joinEvent(eventId: string): Promise<void> {
    const refs = (this.eventRefs.get(eventId) ?? 0) + 1;
    this.eventRefs.set(eventId, refs);
    if (refs > 1) return;

    const hub = await this.connect();
    if (hub?.state === HubConnectionState.Connected) {
      await hub.invoke('JoinEvent', eventId).catch(() => undefined);
    }
  }

  async leaveEvent(eventId: string): Promise<void> {
    const refs = (this.eventRefs.get(eventId) ?? 0) - 1;
    if (refs > 0) {
      this.eventRefs.set(eventId, refs);
      return;
    }
    this.eventRefs.delete(eventId);
    if (this.hub?.state === HubConnectionState.Connected) {
      await this.hub.invoke('LeaveEvent', eventId).catch(() => undefined);
    }
  }

  async joinNotifications(): Promise<void> {
    const hub = await this.connect();
    if (hub?.state === HubConnectionState.Connected) {
      await hub.invoke('JoinMyNotifications').catch(() => undefined);
    }
  }

  private bindHandlers(hub: HubConnection): void {
    if (this.handlersBound) return;
    this.handlersBound = true;

    hub.on('seatStatusChanged', (payload: { eventId: string; seats: SeatChange[] }) => {
      this.seatChanges$.next(payload);
    });
    hub.on('seatStatsChanged', (payload: { eventId: string; stats: SeatStats }) => {
      this.statsChanges$.next(payload);
    });
    hub.on('notification', (notification: AppNotification) => {
      this.notifications$.next(notification);
    });
    hub.onreconnecting(() => this.status.set('connecting'));
    hub.onreconnected(() => this.status.set('live'));
    hub.onclose(() => this.status.set('offline'));
  }
}
