import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { AUTH_CHANGED_EVENT, dispatchAuthChanged } from './authEvents';

describe('authEvents', () => {
  beforeEach(() => {
    vi.stubGlobal('window', {
      dispatchEvent: vi.fn(),
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('exports stable event name', () => {
    expect(AUTH_CHANGED_EVENT).toBe('parkease:auth-changed');
  });

  it('dispatches CustomEvent with detail', () => {
    dispatchAuthChanged({ reason: 'login' });
    expect(window.dispatchEvent).toHaveBeenCalledTimes(1);
    const event = window.dispatchEvent.mock.calls[0][0];
    expect(event).toBeInstanceOf(CustomEvent);
    expect(event.type).toBe(AUTH_CHANGED_EVENT);
    expect(event.detail).toEqual({ reason: 'login' });
  });

  it('no-ops when window is undefined', () => {
    vi.stubGlobal('window', undefined);
    expect(() => dispatchAuthChanged()).not.toThrow();
  });
});
