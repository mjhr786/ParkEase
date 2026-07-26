import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';

export default function AdminListingDetail() {
  const { id } = useParams();
  const [listing, setListing] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminListing(id);
      if (res?.success) setListing(res.data);
      else toast.error(res?.message || 'Listing not found');
    } catch (e) {
      toast.error(e.message || 'Failed to load listing');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const run = async (action) => {
    if (!reason.trim()) {
      toast.error('Reason is required');
      return;
    }
    setBusy(true);
    try {
      let res;
      if (action === 'activate') res = await api.activateAdminListing(id, reason.trim());
      else if (action === 'deactivate') res = await api.deactivateAdminListing(id, reason.trim());
      else if (action === 'verify') res = await api.verifyAdminListing(id, reason.trim());
      else res = await api.unverifyAdminListing(id, reason.trim());

      if (res?.success) {
        setListing(res.data);
        setReason('');
        toast.success(res.message || 'Updated');
      } else {
        toast.error(res?.message || 'Action failed');
      }
    } catch (e) {
      toast.error(e.message || 'Action failed');
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '3rem' }}>
        <div className="spinner" />
      </div>
    );
  }

  if (!listing) {
    return (
      <div>
        <Link to="/admin/listings" style={{ color: 'var(--color-accent-light)' }}>← Back to listings</Link>
        <p style={{ color: 'var(--color-text-secondary)', marginTop: '1rem' }}>Listing not found.</p>
      </div>
    );
  }

  return (
    <div>
      <Link to="/admin/listings" style={{ color: 'var(--color-accent-light)', fontSize: '0.9rem', textDecoration: 'none' }}>
        ← Back to listings
      </Link>
      <header style={{ margin: '0.75rem 0 1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>{listing.title}</h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)' }}>
          {listing.address}, {listing.city}, {listing.state}
        </p>
      </header>

      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
        gap: '1rem',
        marginBottom: '1.25rem',
      }}>
        <div style={card}>
          <h2 style={cardTitle}>Details</h2>
          <Row label="Owner" value={<Link to={`/admin/users/${listing.ownerId}`} style={{ color: 'var(--color-accent-light)' }}>{listing.ownerId?.slice(0, 8)}…</Link>} />
          <Row label="Active" value={listing.isActive ? 'Yes' : 'No'} />
          <Row label="Verified" value={listing.isVerified ? 'Yes' : 'No'} />
          <Row label="Corporate only" value={listing.isCorporateOnly ? 'Yes' : 'No'} />
          <Row label="Spots" value={`${listing.availableSpots ?? '—'} / ${listing.totalSpots ?? '—'}`} />
          <Row label="Hourly" value={listing.hourlyRate != null ? Number(listing.hourlyRate).toFixed(2) : '—'} />
          <Row label="Daily" value={listing.dailyRate != null ? Number(listing.dailyRate).toFixed(2) : '—'} />
          <Row label="Zone" value={listing.zoneCode || '—'} />
        </div>

        <div style={{ ...card, border: '1px solid rgba(248,113,113,0.15)' }}>
          <h2 style={{ ...cardTitle, color: 'var(--color-error)' }}>Moderation</h2>
          <p style={{ margin: '0 0 0.85rem', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
            Deactivate removes the listing from public search. Verify is an ops badge (search does not require it).
          </p>
          <label style={{ display: 'block', fontSize: '0.8rem', color: 'var(--color-text-secondary)', marginBottom: '0.35rem' }}>
            Reason
          </label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
            maxLength={500}
            placeholder="e.g. Spam listing / incomplete address"
            style={textareaStyle}
          />
          <div style={{ display: 'flex', gap: '0.6rem', flexWrap: 'wrap' }}>
            {listing.isActive ? (
              <button type="button" className="btn btn-primary" disabled={busy} onClick={() => run('deactivate')} style={{ background: 'var(--color-error)', borderColor: 'var(--color-error)' }}>
                {busy ? 'Working…' : 'Deactivate'}
              </button>
            ) : (
              <button type="button" className="btn btn-primary" disabled={busy} onClick={() => run('activate')}>
                {busy ? 'Working…' : 'Activate'}
              </button>
            )}
            {listing.isVerified ? (
              <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => run('unverify')}>
                Unverify
              </button>
            ) : (
              <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => run('verify')}>
                Verify
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function Row({ label, value }) {
  return (
    <div style={{
      display: 'flex',
      justifyContent: 'space-between',
      gap: '1rem',
      padding: '0.4rem 0',
      borderBottom: '1px solid var(--color-border)',
      fontSize: '0.875rem',
    }}>
      <span style={{ color: 'var(--color-text-secondary)' }}>{label}</span>
      <span style={{ textAlign: 'right' }}>{value}</span>
    </div>
  );
}

const card = {
  background: 'var(--color-surface)',
  borderRadius: '14px',
  border: '1px solid var(--color-border)',
  padding: '1.15rem 1.25rem',
};

const cardTitle = { margin: '0 0 0.75rem', fontSize: '0.95rem', color: 'var(--color-text-secondary)' };

const textareaStyle = {
  width: '100%',
  boxSizing: 'border-box',
  background: 'var(--color-bg-primary)',
  border: '1px solid var(--color-border)',
  borderRadius: '10px',
  padding: '0.65rem 0.75rem',
  color: 'var(--color-text-primary)',
  resize: 'vertical',
  marginBottom: '0.85rem',
};
