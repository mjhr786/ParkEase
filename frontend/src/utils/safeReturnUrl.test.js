import { describe, it, expect } from 'vitest';
import { safeReturnUrl } from './safeReturnUrl';

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
