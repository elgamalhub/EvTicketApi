import { Component, computed, input, signal } from '@angular/core';
import { ChartPoint } from '../core/models';
import { money } from '../core/format';

interface PlotPoint {
  x: number;
  y: number;
  point: ChartPoint;
}

@Component({
  selector: 'app-area-chart',
  template: `
    <div class="chart">
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height()" role="img" [attr.aria-label]="label()">
        <defs>
          <linearGradient [attr.id]="gradientId()" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" [attr.stop-color]="color()" stop-opacity="0.36" />
            <stop offset="100%" [attr.stop-color]="color()" stop-opacity="0" />
          </linearGradient>
        </defs>

        @for (tick of yTicks(); track tick.y) {
          <line class="grid" [attr.x1]="pad.left" [attr.x2]="width - pad.right" [attr.y1]="tick.y" [attr.y2]="tick.y" />
          <text class="y-label" [attr.x]="pad.left - 10" [attr.y]="tick.y + 3.5" text-anchor="end">{{ tick.label }}</text>
        }

        @if (plot().length > 0) {
          <path [attr.d]="areaPath()" [attr.fill]="'url(#' + gradientId() + ')'" />
          <path [attr.d]="linePath()" fill="none" [attr.stroke]="color()" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" />

          @for (p of plot(); track p.point.label; let i = $index) {
            <g class="hit" (mouseenter)="hovered.set(i)" (mouseleave)="hovered.set(null)">
              <rect
                [attr.x]="p.x - 18"
                [attr.y]="pad.top"
                width="36"
                [attr.height]="plotHeight()"
                fill="transparent"
              />
              @if (hovered() === i) {
                <line class="guide" [attr.x1]="p.x" [attr.x2]="p.x" [attr.y1]="p.y" [attr.y2]="height() - pad.bottom" />
                <circle [attr.cx]="p.x" [attr.cy]="p.y" r="5" [attr.fill]="color()" stroke="#0a1020" stroke-width="2.5" />
              }
            </g>
          }

          @for (tick of xTicks(); track tick.x) {
            <text class="x-label" [attr.x]="tick.x" [attr.y]="height() - 7" text-anchor="middle">{{ tick.label }}</text>
          }
        }
      </svg>

      @if (hoveredPoint(); as p) {
        <div
          class="tooltip"
          [style.left.%]="(p.x / width) * 100"
          [style.top.%]="(p.y / height()) * 100"
        >
          <span>{{ p.point.label }}</span>
          <strong>{{ format(p.point.value) }}</strong>
          @if (p.point.count !== undefined && p.point.count > 0) {
            <em>{{ p.point.count }} orders</em>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .chart { position: relative; width: 100%; }
      svg { width: 100%; height: auto; overflow: visible; }
      .grid { stroke: rgba(255, 255, 255, 0.07); stroke-width: 1; stroke-dasharray: 3 5; }
      .y-label, .x-label { fill: #7b87a6; font-size: 10.5px; font-family: inherit; }
      .guide { stroke: rgba(255, 255, 255, 0.22); stroke-width: 1; stroke-dasharray: 4 4; }
      .hit { cursor: crosshair; }
      .tooltip {
        position: absolute;
        transform: translate(-50%, calc(-100% - 10px));
        min-width: 92px;
        padding: 8px 11px;
        border: 1px solid var(--border-strong);
        border-radius: 10px;
        background: rgba(10, 15, 30, 0.97);
        box-shadow: 0 12px 30px rgba(0, 0, 0, 0.5);
        pointer-events: none;
        text-align: center;
        z-index: 4;
        display: flex;
        flex-direction: column;
        gap: 1px;
      }
      .tooltip span { color: var(--muted); font-size: 10.5px; }
      .tooltip strong { color: var(--text); font-size: 13px; font-family: var(--font-display); }
      .tooltip em { color: #64708c; font-size: 10px; font-style: normal; }
    `
  ]
})
export class AreaChart {
  readonly data = input.required<ChartPoint[]>();
  readonly height = input(220);
  readonly currency = input('USD');
  readonly mode = input<'currency' | 'number'>('currency');
  readonly color = input('#8f7dff');
  readonly label = input('Trend chart');

  readonly width = 640;
  readonly pad = { top: 18, right: 14, bottom: 30, left: 48 };
  readonly hovered = signal<number | null>(null);

  readonly plot = computed<PlotPoint[]>(() => {
    const data = this.data();
    if (data.length === 0) return [];

    const max = Math.max(...data.map((point) => point.value), 1);
    const innerWidth = this.width - this.pad.left - this.pad.right;
    const innerHeight = this.plotHeight();
    const step = data.length === 1 ? 0 : innerWidth / (data.length - 1);

    return data.map((point, index) => ({
      x: this.pad.left + index * step,
      y: this.pad.top + (1 - point.value / max) * innerHeight,
      point
    }));
  });

  readonly hoveredPoint = computed(() => {
    const index = this.hovered();
    return index === null ? null : this.plot()[index] ?? null;
  });

  readonly linePath = computed(() => smoothPath(this.plot()));
  readonly areaPath = computed(() => {
    const points = this.plot();
    if (points.length === 0) return '';
    const bottom = this.height() - this.pad.bottom;
    const first = points[0];
    const last = points[points.length - 1];
    return `${smoothPath(points)} L ${last.x} ${bottom} L ${first.x} ${bottom} Z`;
  });

  readonly yTicks = computed(() => {
    const max = Math.max(...this.data().map((point) => point.value), 1);
    const innerHeight = this.plotHeight();
    return [0, 1, 2, 3].map((index) => {
      const value = (max * (3 - index)) / 3;
      return {
        y: this.pad.top + (innerHeight * index) / 3,
        label: this.compact(value)
      };
    });
  });

  readonly xTicks = computed(() => {
    const points = this.plot();
    if (points.length === 0) return [];
    const stride = Math.max(1, Math.ceil(points.length / 7));
    return points
      .filter((_, index) => index % stride === 0 || index === points.length - 1)
      .map((point) => ({ x: point.x, label: point.point.label }));
  });

  readonly gradientId = computed(() => `chart-fill-${this.color().replace(/[^a-z0-9]/gi, '')}`);

  plotHeight(): number {
    return this.height() - this.pad.top - this.pad.bottom;
  }

  format(value: number): string {
    return this.mode() === 'currency' ? money(value, this.currency()) : new Intl.NumberFormat('en-US').format(value);
  }

  private compact(value: number): string {
    if (this.mode() !== 'currency') {
      return new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(value);
    }
    return money(value, this.currency(), true);
  }
}

function smoothPath(points: PlotPoint[]): string {
  if (points.length === 0) return '';
  if (points.length === 1) return `M ${points[0].x} ${points[0].y}`;

  let path = `M ${points[0].x} ${points[0].y}`;
  for (let i = 0; i < points.length - 1; i++) {
    const before = points[i - 1] ?? points[i];
    const current = points[i];
    const next = points[i + 1];
    const after = points[i + 2] ?? next;

    const control1X = current.x + (next.x - before.x) / 6;
    const control1Y = current.y + (next.y - before.y) / 6;
    const control2X = next.x - (after.x - current.x) / 6;
    const control2Y = next.y - (after.y - current.y) / 6;

    path += ` C ${control1X} ${control1Y}, ${control2X} ${control2Y}, ${next.x} ${next.y}`;
  }
  return path;
}
