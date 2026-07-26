import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';

function statusLabel(s) {
  const map = {
    0: 'Pending',
    1: 'Confirmed',
    2: 'InProgress',
    3: 'Completed',
    4: 'Cancelled',
    5: 'Expired',
    6: 'AwaitingPayment',
    7: 'Rejected',
    8: 'PendingExtension',
    9: 'AwaitingExtensionPayment',
  };
  const n = typeof s === 'number' ? s : Number(s);
  return map[n] || String(s);
}

export default function AdminBookingDetail() {
  const { id } = useParams();
  const [booking, setBooking] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminBooking(id);
      if (res?.success) setBooking(res.data);
      else toast.error(res?.message || 'Booking not found');
    } catch (e) {
      toast.error(e.message || 'Failed to load booking');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const cancel = async () => {
    if (!reason.trim()) {
      toast.error('Reason is required');
      return;
    }
    if (!window.confirm('Force-cancel this booking? This cannot be undone.')) return;
    setBusy(true);
    try {
      const res = await api.cancelAdminBooking(id, reason.trim());
      if (res?.success) {
        setBooking(res.data);
        setReason('');
        toast.success(res.message || 'Booking cancelled');
      } else {
        toast.error(res?.message || 'Cancel failed');
      }
    } catch (e) {
      toast.error(e.message || 'Cancel failed');
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

  if (!booking) {
    return (
      <div>
        <Link to="/admin/bookings" style={{ color: 'var(--color-accent-light)' }}>← Back to bookings</Link>
        <p style={{ color: 'var(--color-text-secondary)', marginTop: '1rem' }}>Booking not found.</p>
      </div>
    );
  }

  const statusName = statusLabel(booking.status);
  const canCancel = statusName !== 'Completed' && statusName !== 'Cancelled';

  return (
    <div>
      <Link to="/admin/bookings" style={{ color: 'var(--color-accent-light)', fontSize: '0.9rem', textDecoration: 'none' }}>
        ← Back to bookings
      </Link>
      <header style={{ margin: '0.75rem 0 1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>
          {booking.bookingReference || booking.id}
        </h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)' }}>
          {booking.parkingSpaceTitle || booking.parkingSpaceId} · {statusLabel(booking.status)}
        </p>
      </header>

      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
        gap: '1rem',
        marginBottom: '1.25rem',
      }}>
        <div style={card}>
          <h2 style={cardTitle}>Booking</h2>
          <Row label="User" value={<Link to={`/admin/users/${booking.userId}`} style={{ color: 'var(--color-accent-light)' }}>{booking.userId?.slice(0, 8)}…</Link>} />
          <Row label="Space" value={<Link to={`/admin/listings/${booking.parkingSpaceId}`} style={{ color: 'var(--color-accent-light)' }}>{booking.parkingSpaceTitle || 'View'}</Link>} />
          <Row label="Start" value={booking.startDateTime ? new Date(booking.startDateTime).toLocaleString() : '—'} />
          <Row label="End" value={booking.endDateTime ? new Date(booking.endDateTime).toLocaleString() : '—'} />
          <Row label="Vehicle" value={booking.vehicleNumber || '—'} />
          <Row label="Total" value={booking.totalAmount != null ? Number(booking.totalAmount).toFixed(2) : '—'} />
          <Row label="Pending extension" value={booking.hasPendingExtension ? 'Yes' : 'No'} />
          {booking.paymentId && (
            <Row
              label="Payment"
              value={
                <Link to={`/admin/payments/${booking.paymentId}`} style={{ color: 'var(--color-accent-light)' }}>
                  {booking.paymentStatus != null ? String(booking.paymentStatus) : 'Open'}
                </Link>
              }
            />
          )}
          {booking.cancellationReason && (
            <Row label="Cancel reason" value={booking.cancellationReason} />
          )}
        </div>

        {canCancel && (
          <div style={{ ...card, border: '1px solid rgba(248,113,113,0.15)' }}>
            <h2 style={{ ...cardTitle, color: 'var(--color-error)' }}>Force cancel</h2>
            <p style={{ margin: '0 0 0.85rem', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
              Cancels the booking, frees event package inventory, and notifies guest + owner.
              Refund remaining balance separately under Payments if needed.
            </p>
            <label style={{ display: 'block', fontSize: '0.8rem', color: 'var(--color-text-secondary)', marginBottom: '0.35rem' }}>
              Reason
            </label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
              maxLength={500}
              placeholder="e.g. Double booking / support ticket #42"
              style={textareaStyle}
            />
            <button
              type="button"
              className="btn btn-primary"
              disabled={busy}
              onClick={cancel}
              style={{ background: 'var(--color-error)', borderColor: 'var(--color-error)' }}
            >
              {busy ? 'Working…' : 'Force cancel booking'}
            </button>
          </div>
        )}
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
      <span style={{ textAlign: 'right', maxWidth: '60%', wordBreak: 'break-word' }}>{value}</span>
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
