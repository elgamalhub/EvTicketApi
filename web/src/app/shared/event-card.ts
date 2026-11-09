import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EventSummary } from '../core/models';
import { dayNumber, money, monthShort, percent, timeOfDay } from '../core/format';
import { Icon } from './icon';

@Component({
  selector: 'app-event-card',
  imports: [RouterLink, Icon],
  templateUrl: './event-card.html',
  styleUrl: './event-card.scss'
})
export class EventCard {
  readonly event = input.required<EventSummary>();

  readonly soldOut = computed(() => this.event().availableTickets <= 0);
  readonly sellThrough = computed(() => percent(this.event().ticketsSold, this.event().capacity));
  readonly month = computed(() => monthShort(this.event().startsAt));
  readonly day = computed(() => dayNumber(this.event().startsAt));
  readonly time = computed(() => timeOfDay(this.event().startsAt));
  readonly money = money;
}
