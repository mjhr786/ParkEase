/**
 * Pure helpers for notification UI (extracted for unit testing).
 */

/** Parse notification.Data — API may send JSON string or object. */
export function parseNotificationData(data) {
  if (!data) return {};
  if (typeof data === 'object') return data;
  try {
    return JSON.parse(data);
  } catch {
    return {};
  }
}

export function isOverstayNotification(data) {
  const t = (data.Type || data.type || '').toString();
  return (
    t === 'booking.overstay' ||
    t === 'booking.overstay.fee' ||
    t === 'booking.overstay.autocheckout'
  );
}

export function isSessionEndingNotification(data) {
  const t = (data.Type || data.type || '').toString();
  return t === 'booking.session.ending';
}

export function isBookingActionNotification(data) {
  return isOverstayNotification(data) || isSessionEndingNotification(data);
}

export function timeAgo(dateStr, nowMs = Date.now()) {
  const diff = (nowMs - new Date(dateStr).getTime()) / 1000;
  if (diff < 60) return 'just now';
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
  return `${Math.floor(diff / 86400)}d ago`;
}

export const TYPE_ICONS = {
  BookingRequest: '📥',
  BookingConfirmed: '✅',
  BookingRejected: '❌',
  PaymentReceived: '💰',
  NewMessage: '💬',
  SystemAlert: '🔔',
  default: '🔔',
};

export const TYPE_COLORS = {
  BookingRequest: '#3b82f6',
  BookingConfirmed: '#10b981',
  BookingRejected: '#ef4444',
  PaymentReceived: '#10b981',
  NewMessage: '#8b5cf6',
  SystemAlert: '#f59e0b',
  default: '#6b7280',
};

export function iconForType(type) {
  return TYPE_ICONS[type] || TYPE_ICONS.default;
}

export function colorForType(type) {
  return TYPE_COLORS[type] || TYPE_COLORS.default;
}
