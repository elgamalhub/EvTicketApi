import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: number;
  kind: ToastKind;
  title: string;
  message?: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private counter = 0;
  private readonly _toasts = signal<Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();

  show(kind: ToastKind, title: string, message?: string, duration = 4200): void {
    const toast: Toast = { id: ++this.counter, kind, title, message };
    this._toasts.update((toasts) => [...toasts.slice(-3), toast]);
    setTimeout(() => this.dismiss(toast.id), duration);
  }

  success(title: string, message?: string): void {
    this.show('success', title, message);
  }

  error(title: string, message?: string): void {
    this.show('error', title, message, 6000);
  }

  info(title: string, message?: string): void {
    this.show('info', title, message);
  }

  warning(title: string, message?: string): void {
    this.show('warning', title, message, 5000);
  }

  dismiss(id: number): void {
    this._toasts.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }
}
