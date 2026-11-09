import { HttpErrorResponse } from '@angular/common/http';

export function money(value: number, currency = 'USD', compact = false): string {
  if (compact && Math.abs(value) >= 1000) {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      notation: 'compact',
      minimumFractionDigits: 0,
      maximumFractionDigits: 1
    }).format(value);
  }
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    maximumFractionDigits: Number.isInteger(value) ? 0 : 2
  }).format(value);
}

export function number(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

export function dateLong(iso: string): string {
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    year: 'numeric'
  }).format(new Date(iso));
}

export function dateShort(iso: string): string {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  }).format(new Date(iso));
}

export function dayMonth(iso: string): string {
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' }).format(new Date(iso));
}

export function dayNumber(iso: string): string {
  return new Intl.DateTimeFormat('en-US', { day: '2-digit' }).format(new Date(iso));
}

export function monthShort(iso: string): string {
  return new Intl.DateTimeFormat('en-US', { month: 'short' }).format(new Date(iso)).toUpperCase();
}

export function timeOfDay(iso: string): string {
  return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' }).format(new Date(iso));
}

export function dateTime(iso: string): string {
  return `${dateShort(iso)} · ${timeOfDay(iso)}`;
}

export function dayAndTime(iso: string): string {
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  }).format(new Date(iso));
}

export function relativeTime(iso: string): string {
  const then = new Date(iso).getTime();
  const diff = then - Date.now();
  const abs = Math.abs(diff);
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 31536000000],
    ['month', 2592000000],
    ['week', 604800000],
    ['day', 86400000],
    ['hour', 3600000],
    ['minute', 60000],
    ['second', 1000]
  ];
  const formatter = new Intl.RelativeTimeFormat('en-US', { numeric: 'auto' });
  for (const [unit, ms] of units) {
    if (abs >= ms || unit === 'second') {
      return formatter.format(Math.round(diff / ms), unit);
    }
  }
  return 'just now';
}

export function countdownParts(target: string | Date, now = Date.now()): { total: number; minutes: string; seconds: string; expired: boolean } {
  const end = typeof target === 'string' ? new Date(target).getTime() : target.getTime();
  const total = Math.max(0, end - now);
  const minutes = Math.floor(total / 60000);
  const seconds = Math.floor((total % 60000) / 1000);
  return {
    total,
    minutes: String(minutes).padStart(2, '0'),
    seconds: String(seconds).padStart(2, '0'),
    expired: total <= 0
  };
}

export function initials(name: string | null | undefined): string {
  if (!name) return 'EV';
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');
}

export function apiError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'Cannot reach the EvTicket API. Is it running?';
    const body = error.error as { message?: string; detail?: string; title?: string } | string | null;
    if (typeof body === 'string' && body.length < 300) return body;
    if (body && typeof body === 'object') return body.message ?? body.detail ?? body.title ?? `Request failed (${error.status}).`;
    return `Request failed (${error.status}).`;
  }
  if (error instanceof Error) return error.message;
  return 'Something went wrong. Please try again.';
}

export function statusLabel(status: string): string {
  return status.toLowerCase() === 'soldout' ? 'Sold out' : status.charAt(0).toUpperCase() + status.slice(1).toLowerCase();
}

export function percent(value: number, total: number): number {
  if (!total) return 0;
  return Math.max(0, Math.min(100, (value / total) * 100));
}

export function seatLabel(section?: string | null, row?: string | null, seat?: number | null): string {
  if (!row) return section ?? 'General admission';
  return `${section ?? 'Seat'} · ${row}${seat ?? ''}`;
}
