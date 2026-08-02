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

function pathOnly(url) {
  return url.split('?')[0].split('#')[0];
}

/**
 * Whether a safe return URL is allowed after signing into the given product channel.
 * Prevents marketplace sessions landing on /corporate/* and vice versa.
 * Invite accept is allowed for both (shared identity entry).
 *
 * @param {string} url
 * @param {'marketplace' | 'corporate'} product
 */
export function isChannelCompatibleReturnUrl(url, product) {
  const path = pathOnly(url);
  if (path.startsWith('/invite/accept/')) return true;
  if (product === 'corporate') {
    return path.startsWith('/corporate/');
  }
  // marketplace — block corporate product routes
  return !path.startsWith('/corporate/');
}

/**
 * Resolve where to send the user after marketplace or corporate auth.
 *
 * Defaults:
 * - marketplace → /dashboard (or /admin for platform admins)
 * - corporate → /corporate/dashboard (or /corporate/create-company when bootstrap)
 *
 * A returnUrl is honored only when safe and channel-compatible.
 *
 * @param {'marketplace' | 'corporate'} product
 * @param {{ returnUrl?: string|null, isAdmin?: boolean, isBootstrap?: boolean }} [opts]
 * @returns {string}
 */
export function postAuthDestination(product, { returnUrl, isAdmin = false, isBootstrap = false } = {}) {
  const safe = safeReturnUrl(returnUrl);
  if (safe && isChannelCompatibleReturnUrl(safe, product)) {
    return safe;
  }

  if (product === 'corporate') {
    return isBootstrap ? '/corporate/create-company' : '/corporate/dashboard';
  }

  return isAdmin ? '/admin' : '/dashboard';
}
