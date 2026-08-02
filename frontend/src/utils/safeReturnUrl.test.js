import { describe, it, expect } from 'vitest';
import {
  safeReturnUrl,
  isChannelCompatibleReturnUrl,
  postAuthDestination,
} from './safeReturnUrl';

describe('safeReturnUrl', () => {
  it('returns null for empty / non-string', () => {
    expect(safeReturnUrl(null)).toBeNull();
    expect(safeReturnUrl(undefined)).toBeNull();
    expect(safeReturnUrl('')).toBeNull();
    expect(safeReturnUrl(42)).toBeNull();
  });

  it('allows same-app relative paths', () => {
    expect(safeReturnUrl('/dashboard')).toBe('/dashboard');
    expect(safeReturnUrl('/invite/accept/abc')).toBe('/invite/accept/abc');
    expect(safeReturnUrl('/corporate/dashboard?x=1')).toBe('/corporate/dashboard?x=1');
  });

  it('blocks open redirects', () => {
    expect(safeReturnUrl('//evil.com')).toBeNull();
    expect(safeReturnUrl('https://evil.com')).toBeNull();
    expect(safeReturnUrl('http://evil.com/path')).toBeNull();
    expect(safeReturnUrl('evil.com')).toBeNull();
  });
});

describe('isChannelCompatibleReturnUrl', () => {
  it('allows invite accept on both channels', () => {
    expect(isChannelCompatibleReturnUrl('/invite/accept/tok', 'marketplace')).toBe(true);
    expect(isChannelCompatibleReturnUrl('/invite/accept/tok', 'corporate')).toBe(true);
  });

  it('blocks corporate routes after marketplace auth', () => {
    expect(isChannelCompatibleReturnUrl('/corporate/dashboard', 'marketplace')).toBe(false);
    expect(isChannelCompatibleReturnUrl('/bookings', 'marketplace')).toBe(true);
  });

  it('requires corporate routes after corporate auth', () => {
    expect(isChannelCompatibleReturnUrl('/corporate/dashboard', 'corporate')).toBe(true);
    expect(isChannelCompatibleReturnUrl('/dashboard', 'corporate')).toBe(false);
  });
});

describe('postAuthDestination', () => {
  it('marketplace defaults to dashboard (admin → /admin)', () => {
    expect(postAuthDestination('marketplace')).toBe('/dashboard');
    expect(postAuthDestination('marketplace', { isAdmin: true })).toBe('/admin');
  });

  it('corporate defaults to corporate dashboard (bootstrap → create-company)', () => {
    expect(postAuthDestination('corporate')).toBe('/corporate/dashboard');
    expect(postAuthDestination('corporate', { isBootstrap: true })).toBe(
      '/corporate/create-company',
    );
  });

  it('honors channel-compatible returnUrl only', () => {
    expect(
      postAuthDestination('marketplace', { returnUrl: '/bookings' }),
    ).toBe('/bookings');
    expect(
      postAuthDestination('marketplace', { returnUrl: '/corporate/dashboard' }),
    ).toBe('/dashboard');
    expect(
      postAuthDestination('corporate', { returnUrl: '/corporate/members' }),
    ).toBe('/corporate/members');
    expect(
      postAuthDestination('corporate', { returnUrl: '/dashboard' }),
    ).toBe('/corporate/dashboard');
  });

  it('blocks open-redirect returnUrl', () => {
    expect(
      postAuthDestination('marketplace', { returnUrl: 'https://evil.com' }),
    ).toBe('/dashboard');
  });
});
