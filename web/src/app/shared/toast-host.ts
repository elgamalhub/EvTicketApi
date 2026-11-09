import { Component, inject } from '@angular/core';
import { ToastService, ToastKind } from '../core/toast.service';
import { Icon } from './icon';

@Component({
  selector: 'app-toast-host',
  imports: [Icon],
  template: `
    <div class="toast-stack" aria-live="polite">
      @for (toast of toasts(); track toast.id) {
        <div class="toast" [class]="'toast toast-' + toast.kind">
          <div class="toast-icon">
            <app-icon [name]="iconFor(toast.kind)" [size]="18" />
          </div>
          <div class="toast-copy">
            <strong>{{ toast.title }}</strong>
            @if (toast.message) {
              <p>{{ toast.message }}</p>
            }
          </div>
          <button class="toast-close" type="button" (click)="service.dismiss(toast.id)" aria-label="Dismiss">
            <app-icon name="x" [size]="15" />
          </button>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .toast-stack {
        position: fixed;
        right: 20px;
        bottom: 20px;
        z-index: 300;
        display: flex;
        flex-direction: column;
        gap: 10px;
        width: min(360px, calc(100vw - 32px));
      }

      .toast {
        display: flex;
        align-items: flex-start;
        gap: 12px;
        padding: 14px 14px 14px 16px;
        border-radius: 14px;
        border: 1px solid var(--border-strong);
        background: rgba(12, 18, 36, 0.96);
        box-shadow: 0 18px 50px rgba(0, 0, 0, 0.5);
        backdrop-filter: blur(16px);
        animation: toast-in 0.28s ease both;
      }

      .toast-success { border-color: rgba(52, 211, 153, 0.36); }
      .toast-error { border-color: rgba(251, 113, 133, 0.36); }
      .toast-warning { border-color: rgba(251, 191, 36, 0.36); }
      .toast-info { border-color: rgba(34, 211, 238, 0.36); }

      .toast-icon {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        display: grid;
        place-items: center;
        flex: none;
      }

      .toast-success .toast-icon { background: rgba(52, 211, 153, 0.14); color: #6ee7b7; }
      .toast-error .toast-icon { background: rgba(251, 113, 133, 0.14); color: #fda4af; }
      .toast-warning .toast-icon { background: rgba(251, 191, 36, 0.14); color: #fcd34d; }
      .toast-info .toast-icon { background: rgba(34, 211, 238, 0.14); color: #67e8f9; }

      .toast-copy { flex: 1; min-width: 0; }
      .toast-copy strong { display: block; font-size: 13.5px; color: var(--text); }
      .toast-copy p { margin: 3px 0 0; font-size: 12.5px; color: var(--muted); line-height: 1.45; }

      .toast-close {
        border: 0;
        background: transparent;
        color: var(--muted);
        padding: 2px;
        border-radius: 6px;
      }

      .toast-close:hover { color: var(--text); background: rgba(255, 255, 255, 0.06); }

      @keyframes toast-in {
        from { opacity: 0; transform: translateY(14px) scale(0.98); }
        to { opacity: 1; transform: translateY(0) scale(1); }
      }
    `
  ]
})
export class ToastHost {
  readonly toasts = inject(ToastService).toasts;
  readonly service = inject(ToastService);

  iconFor(kind: ToastKind): string {
    return kind === 'success' ? 'check-circle' : kind === 'error' ? 'alert-circle' : kind === 'warning' ? 'alert-triangle' : 'info';
  }
}
