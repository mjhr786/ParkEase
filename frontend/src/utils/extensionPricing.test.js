import { describe, it, expect } from 'vitest';
import {
  isDayBasedPricing,
  firstExtensionEndDateOnly,
  extensionPricingStartIso,
  extensionPricingEndIso,
  resolveExtensionEndIso,
  isValidExtensionDate,
  defaultExtensionEnd,
  billableExtensionDays,
  dateOnlyToNoonUtcIso,
} from './extensionPricing';

describe('extensionPricing', () => {
  it('isDayBasedPricing treats daily/weekly/monthly as day-based', () => {
    expect(isDayBasedPricing(0)).toBe(false);
    expect(isDayBasedPricing(1)).toBe(true);
    expect(isDayBasedPricing(2)).toBe(true);
    expect(isDayBasedPricing(3)).toBe(true);
  });

  it('firstExtensionEndDateOnly is the day after booking end', () => {
    // Construct a local Aug 4 end so the next day is stable across TZ
    const localEnd = new Date(2026, 7, 4, 23, 59, 0, 0); // month is 0-indexed
    expect(firstExtensionEndDateOnly(localEnd)).toBe('2026-08-05');
  });

  it('dateOnlyToNoonUtcIso anchors at 12:00 UTC', () => {
    expect(dateOnlyToNoonUtcIso('2026-08-05')).toBe('2026-08-05T12:00:00.000Z');
  });

  it('day-based extension pricing start is next unpaid local day (noon UTC)', () => {
    // Booking ends end-of-day Aug 4 local (same as product UI)
    const localEnd = new Date(2026, 7, 4, 23, 59, 0, 0);
    expect(extensionPricingStartIso(localEnd, 1)).toBe('2026-08-05T12:00:00.000Z');
  });

  it('day-based quote start from IST-style UTC end does not stay on paid day', () => {
    // Aug 4 23:59 IST stored as UTC
    const endUtc = new Date('2026-08-04T18:29:00.000Z');
    const startIso = extensionPricingStartIso(endUtc, 1);
    const endIso = extensionPricingEndIso('2026-08-05', 1);
    // Both anchors on Aug 5 → 1 inclusive UTC calendar day (was 2 with local-midnight start)
    expect(startIso).toBe('2026-08-05T12:00:00.000Z');
    expect(endIso).toBe('2026-08-05T12:00:00.000Z');
    const s = new Date(startIso);
    const e = new Date(endIso);
    const utcDays =
      Math.floor(
        (Date.UTC(e.getUTCFullYear(), e.getUTCMonth(), e.getUTCDate()) -
          Date.UTC(s.getUTCFullYear(), s.getUTCMonth(), s.getUTCDate())) /
          (24 * 60 * 60 * 1000),
      ) + 1;
    expect(utcDays).toBe(1);
  });

  it('hourly extension pricing start is continuous from booking end', () => {
    const localEnd = new Date(2026, 7, 4, 12, 0, 0, 0);
    expect(extensionPricingStartIso(localEnd, 0)).toBe(localEnd.toISOString());
  });

  it('extend end day Aug 5 after Aug 4 end is valid and one extra day', () => {
    const localEnd = new Date(2026, 7, 4, 23, 59, 0, 0);
    expect(isValidExtensionDate(localEnd, '2026-08-05', 1)).toBe(true);
    expect(isValidExtensionDate(localEnd, '2026-08-04', 1)).toBe(false);
    expect(billableExtensionDays(localEnd, '2026-08-05')).toBe(1);
    expect(billableExtensionDays(localEnd, '2026-08-06')).toBe(2);
  });

  it('resolveExtensionEndIso keeps local end-of-day for stored booking end', () => {
    const iso = resolveExtensionEndIso('2026-08-05', 1);
    const d = new Date(iso);
    // Local wall clock should be end of Aug 5
    expect(d.getFullYear()).toBe(2026);
    expect(d.getMonth()).toBe(7);
    expect(d.getDate()).toBe(5);
    expect(d.getHours()).toBe(23);
    expect(d.getMinutes()).toBe(59);
  });

  it('default daily extension end is next calendar day', () => {
    const localEnd = new Date(2026, 7, 4, 23, 59, 0, 0);
    const def = defaultExtensionEnd(localEnd, 1);
    expect(def.getFullYear()).toBe(2026);
    expect(def.getMonth()).toBe(7);
    expect(def.getDate()).toBe(5);
  });
});
