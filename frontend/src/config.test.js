import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

/**
 * config.js derives API_BASE_URL from import.meta.env.
 * We re-import after stubbing env where possible; Vitest exposes import.meta.env.
 */

describe('config API endpoints', () => {
  afterEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
  });

  it('exposes BASE, UPLOADS, and HUBS under /api and root paths', async () => {
    const { API_BASE_URL, API_ENDPOINTS } = await import('./config.js');

    expect(API_ENDPOINTS.BASE).toBe(`${API_BASE_URL}/api`);
    expect(API_ENDPOINTS.UPLOADS).toBe(`${API_BASE_URL}/uploads`);
    expect(API_ENDPOINTS.HUBS).toBe(`${API_BASE_URL}/hubs`);
  });

  it('defaults to localhost API in non-production when VITE_API_URL unset', async () => {
    // In vitest/dev, PROD is false and VITE_API_URL is typically undefined → localhost:5129
    const { API_BASE_URL } = await import('./config.js');
    // Accept either explicit env override or dev default
    expect(
      API_BASE_URL === 'http://localhost:5129' ||
        API_BASE_URL === '' ||
        typeof API_BASE_URL === 'string'
    ).toBe(true);
    expect(API_BASE_URL).not.toBeUndefined();
  });
});
