import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';

const STATUS_OPTIONS = [
  { value: '', label: 'All statuses' },
  { value: '0', label: 'Pending' },
  { value: '1', label: 'Confirmed' },
  { value: '2', label: 'InProgress' },
  { value: '3', label: 'Completed' },
  { value: '4', label: 'Cancelled' },
  { value: '5', label: 'Expired' },
  { value: '6', label: 'AwaitingPayment' },
  { value: '7', label: 'Rejected' },
  { value: '8', label: 'PendingExtension' },
  { value: '9', label: 'AwaitingExtensionPayment' },
];

function statusLabel(s) {
  const n = typeof s === 'number' ? s : Number(s);
  return STATUS_OPTIONS.find((o) => o.value === String(n))?.label || String(s);
}

export default function AdminBookings() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminBookings({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page,
        pageSize: 25,
      });
      if (res?.success) setData(res.data);
      else toast.error(res?.message || 'Failed to load bookings');
    } catch (e) {
      toast.error(e.message || 'Failed to load bookings');
    } finally {
      setLoading(false);
    }
  }, [search, status, page]);

  useEffect(() => {
    load();
  }, [load]);

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div>
      <header style={{ marginBottom: '1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>Bookings</h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
          Find bookings and force-cancel stuck sessions (refund separately under Payments).
        </p>
      </header>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem', alignItems: 'center' }}>
        <input
          type="search"
          placeholder="Reference, plate, space title…"
          value={search}
          onChange={(e) => { setPage(1); setSearch(e.target.value); }}
          style={inputStyle}
        />
        <select value={status} onChange={(e) => { setPage(1); setStatus(e.target.value); }} style={selectStyle}>
          {STATUS_OPTIONS.map((o) => (
            <option key={o.value || 'all'} value={o.value}>{o.label}</option>
          ))}
        </select>
        <button type="button" className="btn btn-secondary" onClick={load}>Refresh</button>
      </div>

      <div style={tableWrap}>
        {loading ? (
          <div style={{ padding: '2.5rem', textAlign: 'center' }}><div className="spinner" /></div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
            <thead>
              <tr style={{ background: 'rgba(0,0,0,0.2)', color: 'var(--color-text-secondary)', textAlign: 'left' }}>
                <th style={th}>Reference</th>
                <th style={th}>Space</th>
                <th style={th}>Status</th>
                <th style={th}>When</th>
                <th style={th}>Total</th>
                <th style={th} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={6} style={{ padding: '1.5rem', color: 'var(--color-text-muted)', textAlign: 'center' }}>
                    No bookings match your filters.
                  </td>
                </tr>
              )}
              {items.map((b) => (
                <tr key={b.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                  <td style={td}>
                    <div style={{ fontWeight: 600 }}>{b.bookingReference || b.id?.slice(0, 8)}</div>
                    <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.8rem' }}>{b.vehicleNumber || '—'}</div>
                  </td>
                  <td style={td}>{b.parkingSpaceTitle || b.parkingSpaceId?.slice(0, 8)}</td>
                  <td style={td}>{statusLabel(b.status)}</td>
                  <td style={td}>
                    <div style={{ fontSize: '0.8rem' }}>
                      {b.startDateTime ? new Date(b.startDateTime).toLocaleString() : '—'}
                    </div>
                  </td>
                  <td style={td}>{b.totalAmount != null ? Number(b.totalAmount).toFixed(2) : '—'}</td>
                  <td style={{ ...td, textAlign: 'right' }}>
                    <Link to={`/admin/bookings/${b.id}`} style={{ color: 'var(--color-accent-light)', textDecoration: 'none', fontWeight: 600 }}>
                      View
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginTop: '1rem',
        color: 'var(--color-text-secondary)',
        fontSize: '0.85rem',
      }}>
        <span>{data ? `${data.totalCount} total` : ''}</span>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button type="button" className="btn btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>Previous</button>
          <span style={{ alignSelf: 'center' }}>Page {page} / {totalPages || 1}</span>
          <button type="button" className="btn btn-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      </div>
    </div>
  );
}

const inputStyle = {
  flex: '1 1 220px',
  background: 'var(--color-surface)',
  border: '1px solid var(--color-border)',
  borderRadius: '10px',
  padding: '0.6rem 0.85rem',
  color: 'var(--color-text-primary)',
};

const selectStyle = {
  background: 'var(--color-surface)',
  border: '1px solid var(--color-border)',
  borderRadius: '10px',
  padding: '0.6rem 0.85rem',
  color: 'var(--color-text-primary)',
};

const tableWrap = {
  background: 'var(--color-surface)',
  borderRadius: '14px',
  border: '1px solid var(--color-border)',
  overflow: 'hidden',
};

const th = { padding: '0.75rem 1rem' };
const td = { padding: '0.8rem 1rem' };
