import { describe, it, expect } from 'vitest';
import { getErrorMessage, handleApiError } from './errorHandler';

describe('getErrorMessage', () => {
  it('joins array errors', () => {
    expect(getErrorMessage({ errors: ['a', 'b'] })).toBe('a, b');
  });

  it('flattens object validation errors', () => {
    expect(
      getErrorMessage({ errors: { email: ['required'], name: ['too short'] } })
    ).toBe('required, too short');
  });

  it('prefers detail then title then message', () => {
    expect(getErrorMessage({ detail: 'd1' })).toBe('d1');
    expect(getErrorMessage({ title: 't1' })).toBe('t1');
    expect(getErrorMessage({ message: 'm1' })).toBe('m1');
  });

  it('falls back when empty', () => {
    expect(getErrorMessage(null)).toBe('An error occurred');
    expect(getErrorMessage({})).toBe('An error occurred');
  });

  it('ignores empty error arrays and non-string object values', () => {
    expect(getErrorMessage({ errors: [] })).toBe('An error occurred');
    expect(getErrorMessage({ errors: { field: [123, ''] }, message: 'fallback-msg' })).toBe(
      'fallback-msg'
    );
  });
});

describe('handleApiError', () => {
  it('reads nested response data', () => {
    expect(
      handleApiError({ response: { data: { message: 'from api' } } })
    ).toBe('from api');
  });

  it('uses Error.message', () => {
    expect(handleApiError(new Error('boom'))).toBe('boom');
  });

  it('uses default message', () => {
    expect(handleApiError({}, 'fallback')).toBe('fallback');
  });
});
