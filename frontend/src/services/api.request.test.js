import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const store = new Map();

function jsonResponse(body, { status = 200, contentType = 'application/json' } = {}) {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: {
      get: (name) => {
        if (name.toLowerCase() === 'content-type') return contentType;
        if (name.toLowerCase() === 'content-disposition') return null;
        return null;
      },
    },
    json: async () => body,
    blob: async () => new Blob(['x']),
  };
}

describe('ApiService request / refresh / handleResponse', () => {
  beforeEach(() => {
    store.clear();
    vi.resetModules();
    vi.stubGlobal('localStorage', {
      getItem: (k) => (store.has(k) ? store.get(k) : null),
      setItem: (k, v) => store.set(k, String(v)),
      removeItem: (k) => store.delete(k),
    });
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('request attaches bearer token and returns JSON data', async () => {
    store.set('accessToken', 'tok-1');
    fetch.mockResolvedValueOnce(
      jsonResponse({ success: true, data: { id: 1 } })
    );

    const { default: api } = await import('./api.js');
    const data = await api.request('/users/me');

    expect(data).toEqual({ success: true, data: { id: 1 } });
    expect(fetch).toHaveBeenCalled();
    const [, opts] = fetch.mock.calls[0];
    expect(opts.headers.Authorization).toBe('Bearer tok-1');
  });

  it('handleResponse throws structured error for JSON failure', async () => {
    fetch.mockResolvedValueOnce(
      jsonResponse({ message: 'bad request', errors: ['x'] }, { status: 400 })
    );

    const { default: api } = await import('./api.js');
    await expect(api.request('/broken')).rejects.toMatchObject({
      message: 'bad request',
      response: { status: 400 },
    });
  });

  it('refreshToken returns false when no refresh token stored', async () => {
    const { default: api } = await import('./api.js');
    await expect(api.refreshToken()).resolves.toBe(false);
    expect(fetch).not.toHaveBeenCalled();
  });

  it('refreshToken stores new tokens on success', async () => {
    store.set('refreshToken', 'old-refresh');
    fetch.mockResolvedValueOnce(
      jsonResponse({
        success: true,
        data: { accessToken: 'new-a', refreshToken: 'new-r' },
      })
    );

    const { default: api } = await import('./api.js');
    await expect(api.refreshToken()).resolves.toBe(true);
    expect(api.getToken()).toBe('new-a');
    expect(store.get('refreshToken')).toBe('new-r');
  });

  it('refreshToken sends stored channel and companyId', async () => {
    store.set('refreshToken', 'old-refresh');
    store.set('channel', 'Corporate');
    store.set('companyId', 'co-9');
    fetch.mockResolvedValueOnce(
      jsonResponse({
        success: true,
        data: {
          accessToken: 'new-a',
          refreshToken: 'new-r',
          channel: 'Corporate',
          companyId: 'co-9',
        },
      })
    );

    const { default: api } = await import('./api.js');
    await expect(api.refreshToken()).resolves.toBe(true);
    const [, opts] = fetch.mock.calls[0];
    const body = JSON.parse(opts.body);
    expect(body.refreshToken).toBe('old-refresh');
    expect(body.channel).toBe('Corporate');
    expect(body.companyId).toBe('co-9');
    expect(store.get('channel')).toBe('Corporate');
  });

  it('loginCorporate posts to /auth/login/corporate', async () => {
    fetch.mockResolvedValueOnce(jsonResponse({ success: true }));
    const { default: api } = await import('./api.js');
    await api.loginCorporate({ email: 'c@b.com', password: 'x', companyId: 'c1' });
    const [url, opts] = fetch.mock.calls[0];
    expect(url).toContain('/auth/login/corporate');
    expect(opts.method).toBe('POST');
    expect(JSON.parse(opts.body)).toEqual({
      email: 'c@b.com',
      password: 'x',
      companyId: 'c1',
    });
  });

  it('handleResponse marks channelForbidden on 403 code', async () => {
    fetch.mockResolvedValueOnce(
      jsonResponse(
        { message: 'denied', code: 'channel_forbidden', errors: ['channel_forbidden'] },
        { status: 403 }
      )
    );

    const { default: api } = await import('./api.js');
    await expect(api.request('/v1/corporate/me/companies')).rejects.toMatchObject({
      channelForbidden: true,
      code: 'channel_forbidden',
      response: { status: 403 },
    });
  });


  it('refreshToken returns false when response not ok', async () => {
    store.set('refreshToken', 'old-refresh');
    fetch.mockResolvedValueOnce(jsonResponse({}, { status: 401 }));

    const { default: api } = await import('./api.js');
    await expect(api.refreshToken()).resolves.toBe(false);
  });

  it('login posts to /auth/login', async () => {
    fetch.mockResolvedValueOnce(jsonResponse({ success: true }));
    const { default: api } = await import('./api.js');
    await api.login({ email: 'a@b.com', password: 'x' });
    const [url, opts] = fetch.mock.calls[0];
    expect(url).toContain('/auth/login');
    expect(opts.method).toBe('POST');
  });

  it('requestBlob returns blob and filename from disposition', async () => {
    store.set('accessToken', 'tok');
    fetch.mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: {
        get: (name) => {
          if (name.toLowerCase() === 'content-type') return 'text/csv';
          if (name.toLowerCase() === 'content-disposition')
            return 'attachment; filename="export.csv"';
          return null;
        },
      },
      json: async () => ({}),
      blob: async () => new Blob(['a,b']),
    });

    const { default: api } = await import('./api.js');
    const result = await api.requestBlob('/export');
    expect(result.fileName).toBe('export.csv');
    expect(result.blob).toBeInstanceOf(Blob);
  });
});
