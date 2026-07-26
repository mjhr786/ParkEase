import { describe, it, expect } from 'vitest';
import { formatInr, formatHours } from './formatCurrency';

describe('formatInr', () => {
  it('formats numbers as INR', () => {
    const s = formatInr(1500);
    expect(s).toMatch(/1[,.]?500|₹/);
    expect(s).toContain('1');
  });

  it('returns fallback for nullish/NaN', () => {
    expect(formatInr(null)).toBe('—');
    expect(formatInr(undefined)).toBe('—');
    expect(formatInr(NaN)).toBe('—');
    expect(formatInr(null, { fallback: 'N/A' })).toBe('N/A');
  });
});

describe('formatHours', () => {
  it('formats fractional and whole hours', () => {
    expect(formatHours(0.5)).toBe('30m');
    expect(formatHours(2)).toBe('2h');
    expect(formatHours(1.5)).toBe('1h 30m');
  });

  it('handles invalid as 0h', () => {
    expect(formatHours(-1)).toBe('0h');
    expect(formatHours('x')).toBe('0h');
  });
});
