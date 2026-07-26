import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';

function roleLabel(role) {
  if (role === 0 || role === 'Admin') return 'Admin';
  return 'User';
}

export default function AdminUserDetail() {
  const { id } = useParams();
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminUser(id);
      if (res?.success) setUser(res.data);
      else toast.error(res?.message || 'User not found');
    } catch (e) {
      toast.error(e.message || 'Failed to load user');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const setActive = async (isActive) => {
    if (!reason.trim()) {
      toast.error('Reason is required');
      return;
    }
    setBusy(true);
    try {
      const res = isActive
        ? await api.activateAdminUser(id, reason.trim())
        : await api.deactivateAdminUser(id, reason.trim());
      if (res?.success) {
        setUser(res.data);
        setReason('');
        toast.success(res.message || (isActive ? 'User activated' : 'User deactivated'));
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

  if (!user) {
    return (
      <div>
        <Link to="/admin/users" style={{ color: 'var(--color-accent-light)' }}>← Back to users</Link>
        <p style={{ color: 'var(--color-text-secondary)', marginTop: '1rem' }}>User not found.</p>
      </div>
    );
  }

  return (
    <div>
      <Link to="/admin/users" style={{ color: 'var(--color-accent-light)', fontSize: '0.9rem', textDecoration: 'none' }}>
        ← Back to users
      </Link>
      <header style={{ margin: '0.75rem 0 1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>
          {user.firstName} {user.lastName}
        </h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)' }}>{user.email}</p>
      </header>

      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
        gap: '1rem',
        marginBottom: '1.25rem',
      }}>
        <div style={{
          background: 'var(--color-surface)',
          borderRadius: '14px',
          border: '1px solid var(--color-border)',
          padding: '1.15rem 1.25rem',
        }}>
          <h2 style={{ margin: '0 0 0.75rem', fontSize: '0.95rem', color: 'var(--color-text-secondary)' }}>Profile</h2>
          <Row label="Phone" value={user.phoneNumber || '—'} />
          <Row label="Role" value={roleLabel(user.role)} />
          <Row
            label="Status"
            value={
              <span style={{ color: user.isActive ? 'var(--color-success)' : 'var(--color-error)', fontWeight: 600 }}>
                {user.isActive ? 'Active' : 'Inactive'}
              </span>
            }
          />
          <Row label="Email verified" value={user.isEmailVerified ? 'Yes' : 'No'} />
          <Row label="Vehicles" value={String(user.vehicleCount ?? 0)} />
          <Row label="Last login" value={user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : '—'} />
          <Row label="Created" value={user.createdAt ? new Date(user.createdAt).toLocaleString() : '—'} />
        </div>

        <div style={{
          background: 'var(--color-surface)',
          borderRadius: '14px',
          border: '1px solid rgba(248,113,113,0.15)',
          padding: '1.15rem 1.25rem',
        }}>
          <h2 style={{ margin: '0 0 0.5rem', fontSize: '0.95rem', color: 'var(--color-error)' }}>Trust & safety</h2>
          <p style={{ margin: '0 0 0.85rem', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
            Deactivate blocks login and revokes refresh tokens. Reason is required and written to the audit log.
          </p>
          <label style={{ display: 'block', fontSize: '0.8rem', color: 'var(--color-text-secondary)', marginBottom: '0.35rem' }}>
            Reason
          </label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
            maxLength={500}
            placeholder="e.g. Fraud report #123 — chargebacks"
            style={{
              width: '100%',
              boxSizing: 'border-box',
              background: 'var(--color-bg-primary)',
              border: '1px solid var(--color-border)',
              borderRadius: '10px',
              padding: '0.65rem 0.75rem',
              color: 'var(--color-text-primary)',
              resize: 'vertical',
              marginBottom: '0.85rem',
            }}
          />
          <div style={{ display: 'flex', gap: '0.6rem', flexWrap: 'wrap' }}>
            {user.isActive ? (
              <button
                type="button"
                className="btn btn-primary"
                disabled={busy}
                onClick={() => setActive(false)}
                style={{ background: 'var(--color-error)', borderColor: 'var(--color-error)' }}
              >
                {busy ? 'Working…' : 'Deactivate user'}
              </button>
            ) : (
              <button
                type="button"
                className="btn btn-primary"
                disabled={busy}
                onClick={() => setActive(true)}
              >
                {busy ? 'Working…' : 'Activate user'}
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
