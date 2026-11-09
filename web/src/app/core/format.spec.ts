import { dayAndTime, initials, money, percent, seatLabel, statusLabel } from './format';

describe('format helpers', () => {
  it('formats whole-dollar amounts without decimals', () => {
    expect(money(45)).toBe('$45');
    expect(money(45.5)).toBe('$45.50');
  });

  it('builds compact currency for large values', () => {
    expect(money(125000, 'USD', true)).toContain('125K');
  });

  it('derives initials from a name', () => {
    expect(initials('Alex Morgan')).toBe('AM');
    expect(initials('Prince')).toBe('P');
    expect(initials(null)).toBe('EV');
  });

  it('clamps percentages', () => {
    expect(percent(50, 0)).toBe(0);
    expect(percent(50, 200)).toBe(25);
    expect(percent(300, 200)).toBe(100);
  });

  it('labels seats with a section and row', () => {
    expect(seatLabel('Orchestra', 'C', 12)).toBe('Orchestra · C12');
    expect(seatLabel(null, null, null)).toBe('General admission');
  });

  it('normalises status labels', () => {
    expect(statusLabel('CONFIRMED')).toBe('Confirmed');
    expect(statusLabel('soldout')).toBe('Sold out');
  });

  it('includes the date and time in long timestamps', () => {
    const label = dayAndTime('2026-11-20T20:00:00Z');
    expect(label).toMatch(/Nov/);
    expect(label).toMatch(/\d{1,2}:\d{2}/);
  });
});
