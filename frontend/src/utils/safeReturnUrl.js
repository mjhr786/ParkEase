/**
 * Validate post-login return URLs. Only same-app relative paths are allowed
 * (blocks open redirects to external hosts).
 * @param {unknown} raw
 * @returns {string|null}
 */
export function safeReturnUrl(raw) {
  if (!raw || typeof raw !== 'string') return null;
  if (!raw.startsWith('/') || raw.startsWith('//')) return null;
  return raw;
}
