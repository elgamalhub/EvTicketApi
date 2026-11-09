import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from './icon';

@Component({
  selector: 'app-auth-shell',
  imports: [RouterLink, Icon],
  templateUrl: './auth-shell.html',
  styleUrl: './auth-shell.scss'
})
export class AuthShell {
  readonly eyebrow = input('Welcome back');
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
  readonly sideTitle = input('Real seats. Real nights. No double bookings.');
  readonly sideLede = input(
    'EvTicket locks the exact seat you choose while you pay, so two people can never buy the same spot.'
  );
  readonly points = input<string[]>([
    'Live venue maps with pan and zoom',
    'Seats held for 10 minutes at checkout',
    'QR tickets delivered instantly'
  ]);

  seats(row: number): number[] {
    return Array.from({ length: row === 0 || row === 5 ? 9 : 12 }, (_, index) => index);
  }
}
