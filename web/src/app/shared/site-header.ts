import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { NotificationService } from '../core/notification.service';
import { RealtimeService } from '../core/realtime.service';
import { initials, relativeTime } from '../core/format';
import { Icon } from './icon';

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, RouterLinkActive, Icon],
  templateUrl: './site-header.html',
  styleUrl: './site-header.scss'
})
export class SiteHeader {
  readonly auth = inject(AuthService);
  readonly notifications = inject(NotificationService);
  readonly realtime = inject(RealtimeService);

  readonly menuOpen = signal(false);
  readonly bellOpen = signal(false);
  readonly userOpen = signal(false);
  readonly initials = initials;
  readonly relative = relativeTime;

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.notifications.refreshUnread().subscribe({ error: () => undefined });
        void this.realtime.joinNotifications();
      } else {
        this.notifications.reset();
      }
    });
  }

  toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  toggleBell(): void {
    this.bellOpen.update((open) => !open);
    this.userOpen.set(false);
    if (!this.bellOpen()) return;
    this.notifications.load().subscribe({ error: () => undefined });
  }

  toggleUser(): void {
    this.userOpen.update((open) => !open);
    this.bellOpen.set(false);
  }

  closePanels(): void {
    this.bellOpen.set(false);
    this.userOpen.set(false);
    this.menuOpen.set(false);
  }

  openNotification(link: string | null | undefined, id: string): void {
    if (!link) return;
    this.notifications.markRead(id).subscribe({ error: () => undefined });
    this.closePanels();
  }

  markAllRead(): void {
    this.notifications.markAllRead().subscribe({ error: () => undefined });
  }

  signOut(): void {
    this.closePanels();
    this.auth.logout(true);
  }
}
