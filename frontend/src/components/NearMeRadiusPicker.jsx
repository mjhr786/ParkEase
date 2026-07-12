export const NEAR_ME_RADIUS_OPTIONS_KM = [1, 2, 5, 10, 15, 25, 50];
export const DEFAULT_NEAR_ME_RADIUS_KM = 5;

/**
 * Modal to pick a search radius before using current location ("Near Me").
 */
export default function NearMeRadiusPicker({
    open,
    radiusKm = DEFAULT_NEAR_ME_RADIUS_KM,
    onRadiusChange,
    onConfirm,
    onCancel,
    loading = false,
}) {
    if (!open) return null;

    const selected = Number(radiusKm) || DEFAULT_NEAR_ME_RADIUS_KM;

    return (
        <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="near-me-radius-title"
            onClick={(e) => {
                if (e.target === e.currentTarget && !loading) onCancel?.();
            }}
            style={{
                position: 'fixed',
                inset: 0,
                zIndex: 1100,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: '1rem',
                background: 'rgba(0, 0, 0, 0.65)',
                backdropFilter: 'blur(4px)',
            }}
        >
            <div
                style={{
                    width: '100%',
                    maxWidth: '420px',
                    background: 'var(--color-surface, #1e293b)',
                    border: '1px solid var(--color-border, rgba(255,255,255,0.1))',
                    borderRadius: 'var(--radius-lg, 16px)',
                    padding: '1.5rem',
                    boxShadow: '0 20px 50px rgba(0,0,0,0.45)',
                }}
            >
                <h2
                    id="near-me-radius-title"
                    style={{ margin: '0 0 0.35rem', color: 'var(--color-text, #f1f5f9)', fontSize: '1.25rem' }}
                >
                    Search near you
                </h2>
                <p style={{ margin: '0 0 1.25rem', color: 'var(--color-text-muted, #94a3b8)', fontSize: '0.95rem' }}>
                    Choose how far from your current location to look for parking.
                </p>

                <label className="form-label" htmlFor="near-me-radius-select" style={{ display: 'block', marginBottom: '0.5rem' }}>
                    Search radius
                </label>
                <select
                    id="near-me-radius-select"
                    className="form-select"
                    value={selected}
                    disabled={loading}
                    onChange={(e) => onRadiusChange?.(Number(e.target.value))}
                    style={{ width: '100%', marginBottom: '1rem' }}
                >
                    {NEAR_ME_RADIUS_OPTIONS_KM.map((km) => (
                        <option key={km} value={km}>
                            {km} km
                        </option>
                    ))}
                </select>

                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '1.5rem' }}>
                    {NEAR_ME_RADIUS_OPTIONS_KM.map((km) => {
                        const isActive = selected === km;
                        return (
                            <button
                                key={km}
                                type="button"
                                disabled={loading}
                                onClick={() => onRadiusChange?.(km)}
                                aria-pressed={isActive}
                                style={{
                                    border: isActive
                                        ? '1px solid var(--color-primary, #8b5cf6)'
                                        : '1px solid var(--color-border, #334155)',
                                    background: isActive
                                        ? 'rgba(139, 92, 246, 0.2)'
                                        : 'var(--color-bg-glass, #0f172a)',
                                    color: isActive ? 'var(--color-primary, #a78bfa)' : 'var(--color-text, #e2e8f0)',
                                    borderRadius: '999px',
                                    padding: '0.4rem 0.85rem',
                                    cursor: loading ? 'not-allowed' : 'pointer',
                                    fontWeight: isActive ? 600 : 500,
                                    fontSize: '0.9rem',
                                }}
                            >
                                {km} km
                            </button>
                        );
                    })}
                </div>

                <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                    <button type="button" className="btn btn-secondary" onClick={onCancel} disabled={loading}>
                        Cancel
                    </button>
                    <button type="button" className="btn btn-primary" onClick={onConfirm} disabled={loading}>
                        {loading ? 'Locating…' : `📍 Find within ${selected} km`}
                    </button>
                </div>
            </div>
        </div>
    );
}
