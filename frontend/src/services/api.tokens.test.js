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
    localStorage.setItem('channel', 'Corporate');
    localStorage.setItem('companyId', 'c1');
    api.clearTokens();
    expect(api.getToken()).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(localStorage.getItem('user')).toBeNull();
    expect(localStorage.getItem('channel')).toBeNull();
    expect(localStorage.getItem('companyId')).toBeNull();
  });

  it('applySession stores channel bind and company cache for Corporate', async () => {
    const mod = await import('./api.js');
    const api = mod.default;
    api.applySession({
      accessToken: 'at',
      refreshToken: 'rt',
      channel: 'Corporate',
      companyId: 'co-1',
      companyRole: 'Admin',
      isBootstrap: false,
      user: { email: 'u@t.com', role: 1 },
    });
    expect(api.getToken()).toBe('at');
    expect(localStorage.getItem('channel')).toBe('Corporate');
    expect(localStorage.getItem('companyId')).toBe('co-1');
    expect(localStorage.getItem('companyRole')).toBe('Admin');
    expect(localStorage.getItem('isBootstrap')).toBe('false');
    expect(localStorage.getItem('activeCompanyId')).toBe('co-1');
  });

  it('applySession clears company cache on Marketplace', async () => {
    localStorage.setItem('activeCompanyId', 'old');
    const mod = await import('./api.js');
    const api = mod.default;
    api.applySession({
      accessToken: 'at',
      refreshToken: 'rt',
      channel: 'Marketplace',
      user: { email: 'u@t.com' },
    });
    expect(localStorage.getItem('activeCompanyId')).toBeNull();
    expect(localStorage.getItem('channel')).toBe('Marketplace');
  });

  it('isChannelForbidden detects code and errors token', async () => {
    const mod = await import('./api.js');
    const ApiService = mod.default.constructor;
    expect(ApiService.isChannelForbidden({ code: 'channel_forbidden' })).toBe(true);
    expect(ApiService.isChannelForbidden({ errors: ['channel_forbidden'] })).toBe(true);
    expect(ApiService.isChannelForbidden({ code: 'other' })).toBe(false);
  });
});
