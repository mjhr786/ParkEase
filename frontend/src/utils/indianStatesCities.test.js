import { describe, it, expect } from 'vitest';
import INDIAN_STATES_CITIES, { STATES } from './indianStatesCities';

describe('INDIAN_STATES_CITIES', () => {
  it('includes major states with cities', () => {
    expect(INDIAN_STATES_CITIES['Karnataka']).toContain('Bengaluru');
    expect(INDIAN_STATES_CITIES['Maharashtra']).toContain('Mumbai');
    expect(INDIAN_STATES_CITIES['Delhi']).toBeDefined();
  });

  it('has non-empty city lists for every state', () => {
    const entries = Object.entries(INDIAN_STATES_CITIES);
    expect(entries.length).toBeGreaterThan(20);
    for (const [state, cities] of entries) {
      expect(Array.isArray(cities), state).toBe(true);
      expect(cities.length, state).toBeGreaterThan(0);
    }
  });

  it('STATES is sorted unique keys of the map', () => {
    expect(STATES).toEqual(Object.keys(INDIAN_STATES_CITIES).sort());
    expect(STATES[0] < STATES[STATES.length - 1]).toBe(true);
  });
});
