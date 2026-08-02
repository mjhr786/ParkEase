/**
 * Marketplace vs Corporate product selector for auth pages.
 * @param {'marketplace' | 'corporate'} value
 * @param {(next: 'marketplace' | 'corporate') => void} onChange
 */
export default function AuthChannelSelector({ value, onChange }) {
    return (
        <div className="auth-channel-selector" role="tablist" aria-label="Account type">
            <button
                type="button"
                role="tab"
                aria-selected={value === 'marketplace'}
                className={`auth-channel-option${value === 'marketplace' ? ' active' : ''}`}
                onClick={() => onChange('marketplace')}
            >
                Marketplace
            </button>
            <button
                type="button"
                role="tab"
                aria-selected={value === 'corporate'}
                className={`auth-channel-option${value === 'corporate' ? ' active' : ''}`}
                onClick={() => onChange('corporate')}
            >
                Corporate
            </button>
        </div>
    );
}

/** Parse ?channel= from the URL (defaults to marketplace). */
export function channelFromSearchParams(searchParams) {
    const raw = (searchParams.get('channel') || '').toLowerCase();
    return raw === 'corporate' ? 'corporate' : 'marketplace';
}

/** Build login/register path preserving returnUrl + channel. */
export function authPath(base, { channel, returnUrl } = {}) {
    const params = new URLSearchParams();
    if (channel === 'corporate') params.set('channel', 'corporate');
    if (returnUrl) params.set('returnUrl', returnUrl);
    const qs = params.toString();
    return qs ? `${base}?${qs}` : base;
}
