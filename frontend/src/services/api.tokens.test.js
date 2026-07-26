import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

/**
 * Lightweight unit tests for ApiService token helpers.
 * Uses dynamic import after mocking localStorage / authEvents.
 */

const store = new Map();

describe('ApiService token helpers', () => {
  beforeEach(() => {
    store.clear();
    vi.resetModules();
    vi.stubGlobal('localStorage', {
      getItem: (k) => (store.has(k) ? store.get(k) : null),
      setItem: (k, v) => store.set(k, String(v)),
      removeItem: (k) => store.delete(k),
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('setTokens stores access and refresh tokens', async () => {
    const mod = await import('./api.js');
    // default export is singleton instance
    const api = mod.default;
    api.setTokens('access-1', 'refresh-1');
    expect(api.getToken()).toBe('access-1');
    expect(localStorage.getItem('refreshToken')).toBe('refresh-1');
  });

  it('clearTokens removes session keys', async () => {
    const mod = await import('./api.js');
    const api = mod.default;
    api.setTokens('a', 'r');
    localStorage.setItem('user', '{}');
    api.clearTokens();
    expect(api.getToken()).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(localStorage.getItem('user')).toBeNull();
  });
});
