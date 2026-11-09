import { Component, effect, input, signal } from '@angular/core';
import QRCode from 'qrcode';

@Component({
  selector: 'app-qr-code',
  template: `
    <div class="qr" [style.width.px]="size()">
      @if (src()) {
        <img [src]="src()" [alt]="'QR code for ' + value()" [style.width.px]="size()" [style.height.px]="size()" />
      } @else {
        <div class="qr-loading" [style.width.px]="size()" [style.height.px]="size()"></div>
      }
    </div>
  `,
  styles: [
    `
      :host { display: inline-block; }
      .qr {
        display: grid;
        place-items: center;
        padding: 8px;
        border-radius: 14px;
        background: #fff;
        box-shadow: 0 8px 24px rgba(0, 0, 0, 0.28);
      }
      .qr img { display: block; border-radius: 4px; }
      .qr-loading {
        border-radius: 4px;
        background: repeating-linear-gradient(45deg, #eef1f8, #eef1f8 8px, #e3e8f4 8px, #e3e8f4 16px);
        animation: qr-pulse 1.4s ease-in-out infinite;
      }
      @keyframes qr-pulse {
        0%, 100% { opacity: 1; }
        50% { opacity: 0.55; }
      }
    `
  ]
})
export class QrCode {
  readonly value = input.required<string>();
  readonly size = input(180);
  readonly src = signal<string | null>(null);

  private renderToken = 0;

  constructor() {
    effect(() => {
      const value = this.value();
      const size = this.size();
      if (!value) {
        this.src.set(null);
        return;
      }

      const token = ++this.renderToken;
      QRCode.toDataURL(value, {
        errorCorrectionLevel: 'M',
        margin: 1,
        width: Math.max(120, size),
        color: { dark: '#080c18', light: '#ffffff' }
      })
        .then((url) => {
          if (token === this.renderToken) this.src.set(url);
        })
        .catch(() => {
          if (token === this.renderToken) this.src.set(null);
        });
    });
  }
}
