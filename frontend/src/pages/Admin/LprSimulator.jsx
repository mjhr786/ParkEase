import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';

export default function LprSimulator() {
  const { isAuthenticated, isAdmin, loading: authLoading } = useAuth();
  const navigate = useNavigate();

  const [licensePlate, setLicensePlate] = useState('');
  const [parkingSpaceId, setParkingSpaceId] = useState('');
  const [manualId, setManualId] = useState(false);
  const [listings, setListings] = useState([]);
  const [listingsLoading, setListingsLoading] = useState(true);
  const [direction, setDirection] = useState('Entry');
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);

  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      navigate('/login');
    }
  }, [authLoading, isAuthenticated, navigate]);

  useEffect(() => {
    if (!isAuthenticated) return undefined;
    let cancelled = false;
    (async () => {
      setListingsLoading(true);
      try {
        const res = await api.getMyListings();
        if (cancelled) return;
        const all = res?.success && Array.isArray(res.data) ? res.data : [];
        const lprLots = all.filter((l) => l.isLprEnabled);
        setListings(lprLots);
        if (lprLots.length === 1) {
          setParkingSpaceId((prev) => prev || lprLots[0].id);
        }
      } catch {
        if (!cancelled) setListings([]);
      } finally {
        if (!cancelled) setListingsLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [isAuthenticated]);

  if (!authLoading && !isAuthenticated) {
    return null;
  }

  const onSubmit = async (e) => {
    e.preventDefault();
    if (!licensePlate.trim() || !parkingSpaceId.trim()) {
      toast.error('License plate and parking space are required');
      return;
    }

    setBusy(true);
    setResult(null);
    try {
      const res = await api.simulateLprEvent({
        licensePlate: licensePlate.trim(),
        parkingSpaceId: parkingSpaceId.trim(),
        direction,
      });

      if (!res) return;

      setResult(res.data ?? res);
      if (res.data?.accessGranted) {
        toast.success(res.message || 'Access granted');
      } else if (res.success && res.data && !res.data.accessGranted) {
        toast.error(res.data.denialMessage || res.message || 'Access denied');
      } else if (!res.success) {
        toast.error(res.message || 'Request failed');
      } else {
        toast.success(res.message || 'Done');
      }
    } catch (err) {
      toast.error(err.message || 'Simulator request failed');
    } finally {
      setBusy(false);
    }
  };

  const card = {
    background: '#1e293b',
    borderRadius: '12px',
    padding: '1.25rem 1.5rem',
    border: '1px solid rgba(255,255,255,0.06)',
  };

  return (
    <div className="container" style={{ maxWidth: 720, padding: '2rem 1rem 4rem' }}>
      <h1 style={{ marginBottom: '0.35rem' }}>LPR Simulator</h1>
      <p style={{ color: 'var(--color-text-muted)', marginBottom: '1.5rem' }}>
        Trigger ticketless entry/exit for a confirmed booking by license plate.
        {isAdmin
          ? ' Admins can simulate any LPR-enabled facility (paste ID if not in your listings).'
          : ' Vendors can only simulate parking spaces they own (LPR must be enabled).'}
      </p>

      <form onSubmit={onSubmit} style={{ ...card, display: 'grid', gap: '1rem', marginBottom: '1.5rem' }}>
        <label style={{ display: 'grid', gap: 6 }}>
          <span style={{ fontSize: '0.85rem', color: '#94a3b8' }}>License plate</span>
          <input
            className="input"
            value={licensePlate}
            onChange={(e) => setLicensePlate(e.target.value)}
            placeholder="KA01AB1234"
            required
          />
        </label>

        <label style={{ display: 'grid', gap: 6 }}>
          <span style={{ fontSize: '0.85rem', color: '#94a3b8' }}>Parking facility (LPR-enabled)</span>
          {listingsLoading ? (
            <div style={{ color: '#94a3b8', fontSize: '0.85rem' }}>Loading your LPR lots…</div>
          ) : listings.length > 0 && !manualId ? (
            <>
              <select
                className="input"
                value={parkingSpaceId}
                onChange={(e) => setParkingSpaceId(e.target.value)}
                required
              >
                <option value="">Select a lot…</option>
                {listings.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.title} ({l.city || '—'})
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="btn btn-outline"
                style={{ fontSize: '0.8rem', justifySelf: 'start' }}
                onClick={() => setManualId(true)}
              >
                Enter facility ID manually
              </button>
            </>
          ) : (
            <>
              <input
                className="input"
                value={parkingSpaceId}
                onChange={(e) => setParkingSpaceId(e.target.value)}
                placeholder="Guid of the parking facility"
                required
              />
              {listings.length > 0 && (
                <button
                  type="button"
                  className="btn btn-outline"
                  style={{ fontSize: '0.8rem', justifySelf: 'start' }}
                  onClick={() => setManualId(false)}
                >
                  Choose from my LPR lots
                </button>
              )}
              {listings.length === 0 && (
                <p style={{ margin: 0, fontSize: '0.8rem', color: '#94a3b8' }}>
                  No LPR-enabled listings found.{' '}
                  <Link to="/my/listings" style={{ color: '#93c5fd' }}>Enable LPR on a listing</Link>
                  {isAdmin ? ' or paste a facility GUID above.' : '.'}
                </p>
              )}
            </>
          )}
        </label>

        <label style={{ display: 'grid', gap: 6 }}>
          <span style={{ fontSize: '0.85rem', color: '#94a3b8' }}>Direction</span>
          <select
            className="input"
            value={direction}
            onChange={(e) => setDirection(e.target.value)}
          >
            <option value="Entry">Entry (check-in)</option>
            <option value="Exit">Exit (check-out)</option>
          </select>
        </label>

        <button type="submit" className="btn btn-primary" disabled={busy}>
          {busy ? 'Processing…' : 'Simulate LPR event'}
        </button>
      </form>

      {result && (
        <div style={{
          ...card,
          borderColor: result.accessGranted ? 'rgba(52,211,153,0.4)' : 'rgba(248,113,113,0.4)',
        }}>
          <div style={{
            fontSize: '1.1rem',
            fontWeight: 700,
            color: result.accessGranted ? '#34d399' : '#f87171',
            marginBottom: '0.75rem',
          }}>
            {result.accessGranted ? 'Access granted' : 'Access denied'}
          </div>
          <dl style={{ display: 'grid', gridTemplateColumns: '160px 1fr', gap: '0.4rem 1rem', margin: 0, fontSize: '0.9rem' }}>
            <dt style={{ color: '#94a3b8' }}>Decision</dt>
            <dd style={{ margin: 0 }}>{result.decision}</dd>
            <dt style={{ color: '#94a3b8' }}>Plate</dt>
            <dd style={{ margin: 0 }}>{result.licensePlateNormalized}</dd>
            <dt style={{ color: '#94a3b8' }}>Direction</dt>
            <dd style={{ margin: 0 }}>{result.direction}</dd>
            <dt style={{ color: '#94a3b8' }}>Booking ref</dt>
            <dd style={{ margin: 0 }}>{result.bookingReference || '—'}</dd>
            <dt style={{ color: '#94a3b8' }}>Booking ID</dt>
            <dd style={{ margin: 0, wordBreak: 'break-all' }}>{result.bookingId || '—'}</dd>
            <dt style={{ color: '#94a3b8' }}>Denial code</dt>
            <dd style={{ margin: 0 }}>{result.denialReasonCode || '—'}</dd>
            <dt style={{ color: '#94a3b8' }}>Message</dt>
            <dd style={{ margin: 0 }}>{result.denialMessage || '—'}</dd>
            <dt style={{ color: '#94a3b8' }}>Attempt ID</dt>
            <dd style={{ margin: 0, wordBreak: 'break-all' }}>{result.attemptId || '—'}</dd>
          </dl>
        </div>
      )}
    </div>
  );
}
