/**
 * Format amounts for UI display (INR-oriented parking app).
 * Pure helper — no I/O.
 */
export function formatInr(amount, { fallback = '—' } = {}) {
  if (amount === null || amount === undefined || Number.isNaN(Number(amount))) {
    return fallback;
  }
  const n = Number(amount);
  try {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
    }).format(n);
  } catch {
    return `₹${n.toFixed(2)}`;
  }
}

export function formatHours(hours) {
  const n = Number(hours);
  if (Number.isNaN(n) || n < 0) return '0h';
  if (n < 1) return `${Math.round(n * 60)}m`;
  const whole = Math.floor(n);
  const mins = Math.round((n - whole) * 60);
  return mins > 0 ? `${whole}h ${mins}m` : `${whole}h`;
}
