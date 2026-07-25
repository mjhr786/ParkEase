/**
 * Same-tab auth lifecycle events for SignalR reconnect / disconnect.
 * Cross-tab changes still use the native `storage` event on localStorage.
 *
 * Event name is stable — keep in sync with useNotifications / any future listeners.
 */
export const AUTH_CHANGED_EVENT = 'parkease:auth-changed';

/**
 * Notify listeners that access/refresh tokens (or session) may have changed.
 * Safe to call from non-React code (api.js).
 * @param {{ reason?: string }} [detail]
 */
export function dispatchAuthChanged(detail = {}) {
  try {
    if (typeof window === 'undefined') return;
    window.dispatchEvent(new CustomEvent(AUTH_CHANGED_EVENT, { detail }));
  } catch {
    // Non-browser / restricted environments — ignore
  }
}
