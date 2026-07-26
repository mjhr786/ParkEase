import { describe, it, expect } from 'vitest';
import {
  parseNotificationData,
  isOverstayNotification,
  isSessionEndingNotification,
  isBookingActionNotification,
  timeAgo,
  iconForType,
  colorForType,
} from './notificationHelpers';

describe('parseNotificationData', () => {
  it('returns empty object for null/undefined', () => {
    expect(parseNotificationData(null)).toEqual({});
    expect(parseNotificationData(undefined)).toEqual({});
  });

  it('returns object as-is', () => {
    const o = { type: 'booking.overstay' };
    expect(parseNotificationData(o)).toBe(o);
  });

  it('parses JSON string', () => {
    expect(parseNotificationData('{"Type":"booking.session.ending"}')).toEqual({
      Type: 'booking.session.ending',
    });
  });

  it('returns empty object for invalid JSON', () => {
    expect(parseNotificationData('{not-json')).toEqual({});
  });
});

describe('notification type guards', () => {
  it('detects overstay variants', () => {
    expect(isOverstayNotification({ type: 'booking.overstay' })).toBe(true);
    expect(isOverstayNotification({ Type: 'booking.overstay.fee' })).toBe(true);
    expect(isOverstayNotification({ type: 'booking.overstay.autocheckout' })).toBe(true);
    expect(isOverstayNotification({ type: 'other' })).toBe(false);
  });

  it('detects session ending and booking actions', () => {
    expect(isSessionEndingNotification({ type: 'booking.session.ending' })).toBe(true);
    expect(isBookingActionNotification({ type: 'booking.overstay' })).toBe(true);
    expect(isBookingActionNotification({ type: 'booking.session.ending' })).toBe(true);
    expect(isBookingActionNotification({ type: 'payment.completed' })).toBe(false);
  });
});

describe('timeAgo', () => {
  const now = new Date('2026-07-26T12:00:00Z').getTime();

  it('formats relative windows', () => {
    expect(timeAgo(new Date(now - 10_000).toISOString(), now)).toBe('just now');
    expect(timeAgo(new Date(now - 5 * 60_000).toISOString(), now)).toBe('5m ago');
    expect(timeAgo(new Date(now - 3 * 3600_000).toISOString(), now)).toBe('3h ago');
    expect(timeAgo(new Date(now - 2 * 86400_000).toISOString(), now)).toBe('2d ago');
  });
});

describe('icon and color maps', () => {
  it('resolves known and default types', () => {
    expect(iconForType('BookingConfirmed')).toBe('✅');
    expect(iconForType('Unknown')).toBe('🔔');
    expect(colorForType('BookingRejected')).toBe('#ef4444');
    expect(colorForType('nope')).toBe('#6b7280');
  });
});

describe('type guards with empty payloads', () => {
  it('treats missing type as non-action', () => {
    expect(isOverstayNotification({})).toBe(false);
    expect(isSessionEndingNotification({})).toBe(false);
    expect(isBookingActionNotification({})).toBe(false);
  });
});

