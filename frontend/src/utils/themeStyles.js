/**
 * Shared inline style snippets that follow CSS theme tokens.
 * Prefer these over hardcoded slate/indigo hex in Admin/Corporate/Vendor pages.
 */

export const themeStyles = {
  page: {
    color: 'var(--color-text-primary)',
  },
  card: {
    background: 'var(--color-surface)',
    border: '1px solid var(--color-border)',
    borderRadius: '12px',
  },
  cardTight: {
    background: 'var(--color-surface)',
    border: '1px solid var(--color-border)',
    borderRadius: '8px',
  },
  panel: {
    background: 'var(--color-bg-secondary)',
    border: '1px solid var(--color-border)',
  },
  input: {
    background: 'var(--color-bg-primary)',
    border: '1px solid var(--color-border)',
    borderRadius: '6px',
    color: 'var(--color-text-primary)',
  },
  muted: {
    color: 'var(--color-text-secondary)',
  },
  subtle: {
    color: 'var(--color-text-muted)',
  },
  text: {
    color: 'var(--color-text-primary)',
  },
  link: {
    color: 'var(--color-accent-light)',
    textDecoration: 'none',
  },
  tableHead: {
    color: 'var(--color-text-secondary)',
    textAlign: 'left',
    background: 'var(--color-bg-glass)',
  },
  overlay: {
    position: 'fixed',
    inset: 0,
    background: 'var(--overlay-bg)',
    zIndex: 1000,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '1rem',
  },
  modal: {
    background: 'var(--color-surface)',
    border: '1px solid var(--color-border)',
    borderRadius: '12px',
    padding: '1.5rem',
    width: '100%',
    maxWidth: '520px',
  },
};

/** Common booking / admin status colors (token-backed where possible). */
export const STATUS_COLORS = {
  pending: 'var(--color-warning)',
  confirmed: 'var(--color-success)',
  inProgress: 'var(--color-primary)',
  completed: 'var(--color-success)',
  cancelled: 'var(--color-error)',
  expired: 'var(--color-text-muted)',
  awaitingPayment: 'var(--color-secondary)',
  rejected: 'var(--color-error)',
};

/** Booking status int → color (mirrors domain enum). */
export const BOOKING_STATUS_HEX = {
  0: 'var(--color-warning)',
  1: 'var(--color-success)',
  2: 'var(--color-primary)',
  3: 'var(--color-success)',
  4: 'var(--color-error)',
  5: 'var(--color-text-muted)',
  6: 'var(--color-secondary)',
  7: 'var(--color-error)',
  8: 'var(--color-warning)',
  9: 'var(--color-secondary)',
};

export default themeStyles;
