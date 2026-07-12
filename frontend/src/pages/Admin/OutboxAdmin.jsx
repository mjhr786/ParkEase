import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';

const STATUS_OPTIONS = [
  { value: '', label: 'All statuses' },
  { value: '0', label: 'Pending' },
  { value: '1', label: 'Processing' },
  { value: '2', label: 'Processed' },
  { value: '3', label: 'Failed' },
];

const STATUS_META = {
  0: { label: 'Pending', color: '#fbbf24', bg: 'rgba(251,191,36,0.15)' },
  1: { label: 'Processing', color: '#60a5fa', bg: 'rgba(96,165,250,0.15)' },
  2: { label: 'Processed', color: '#34d399', bg: 'rgba(52,211,153,0.15)' },
  3: { label: 'Failed', color: '#f87171', bg: 'rgba(248,113,113,0.15)' },
};

function StatusBadge({ status }) {
  const meta = STATUS_META[status] || STATUS_META[0];
  return (
    <span style={{
      display: 'inline-block',
      padding: '2px 10px',
      borderRadius: '999px',
      fontSize: '0.75rem',
      fontWeight: 600,
      color: meta.color,
      background: meta.bg,
      border: `1px solid ${meta.color}33`,
    }}>
      {meta.label}
    </span>
  );
}

function SummaryCard({ label, value, tone }) {
  const colors = {
    pending: '#fbbf24',
    processing: '#60a5fa',
    processed: '#34d399',
    failed: '#f87171',
    total: '#a78bfa',
  };
  const color = colors[tone] || colors.total;
  return (
    <div style={{
      flex: '1 1 120px',
      background: '#1e293b',
      borderRadius: '12px',
      padding: '1rem 1.25rem',
      border: '1px solid rgba(255,255,255,0.06)',
    }}>
      <div style={{ color: '#94a3b8', fontSize: '0.8rem', marginBottom: '0.35rem' }}>{label}</div>
      <div style={{ color, fontSize: '1.75rem', fontWeight: 700 }}>{value}</div>
    </div>
  );
}

export default function OutboxAdmin() {
  const { isAdmin, loading: authLoading } = useAuth();
  const navigate = useNavigate();

  const [status, setStatus] = useState('3'); // default Failed
  const [typeFilter, setTypeFilter] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [data, setData] = useState(null);
  const [selected, setSelected] = useState(null);

  const pageSize = 25;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getOutboxMessages({
        status: status === '' ? undefined : Number(status),
        type: typeFilter.trim() || undefined,
        page,
        pageSize,
      });
      if (res?.success) {
        setData(res.data);
      } else {
        toast.error(res?.message || 'Failed to load outbox');
      }
    } catch (err) {
      toast.error(err?.response?.data?.message || err.message || 'Failed to load outbox');
    } finally {
      setLoading(false);
    }
  }, [status, typeFilter, page]);

  useEffect(() => {
    if (!authLoading && !isAdmin) {
      toast.error('Admin access required');
      navigate('/dashboard');
    }
  }, [authLoading, isAdmin, navigate]);

  useEffect(() => {
    if (isAdmin) load();
  }, [isAdmin, load]);

  const handleRequeue = async (id) => {
    setBusyId(id);
    try {
      const res = await api.requeueOutboxMessage(id);
      if (res?.success) {
        toast.success('Message requeued');
        await load();
      } else {
        toast.error(res?.message || 'Requeue failed');
      }
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Requeue failed');
    } finally {
      setBusyId(null);
    }
  };

  const handleRequeueAllFailed = async () => {
    if (!window.confirm('Requeue all failed outbox messages?')) return;
    setBusyId('all');
    try {
      const res = await api.requeueAllFailedOutbox();
      if (res?.success) {
        toast.success(res.message || `Requeued ${res.data} message(s)`);
        setStatus('');
        setPage(1);
        await load();
      } else {
        toast.error(res?.message || 'Failed');
      }
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Failed');
    } finally {
      setBusyId(null);
    }
  };

  const handleProcessNow = async () => {
    setBusyId('process');
    try {
      const res = await api.processOutboxNow(50);
      if (res?.success) {
        toast.success(res.data?.message || `Processed ${res.data?.processedCount ?? 0}`);
        await load();
      } else {
        toast.error(res?.message || 'Process failed');
      }
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Process failed');
    } finally {
      setBusyId(null);
    }
  };

  const openDetail = async (id) => {
    try {
      const res = await api.getOutboxMessage(id);
      if (res?.success) setSelected(res.data);
      else toast.error(res?.message || 'Not found');
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Failed to load message');
    }
  };

  if (authLoading || !isAdmin) {
    return (
      <div className="container" style={{ padding: '3rem', textAlign: 'center' }}>
        <div className="spinner" />
      </div>
    );
  }

  const summary = data?.summary || { pending: 0, processing: 0, processed: 0, failed: 0, total: 0 };
  const items = data?.items || [];
  const totalPages = data?.totalPages || 0;

  return (
    <div className="container" style={{ padding: '2rem 1rem 4rem', maxWidth: '1200px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
        <div>
          <h1 style={{ color: 'white', margin: 0, fontSize: '1.6rem' }}>Outbox Admin</h1>
          <p style={{ color: '#94a3b8', margin: '0.4rem 0 0', fontSize: '0.9rem' }}>
            Failed and pending domain-event side effects (email, push, cache).
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={busyId === 'all'}
            onClick={handleRequeueAllFailed}
          >
            Requeue all failed
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={busyId === 'process'}
            onClick={handleProcessNow}
          >
            Process now
          </button>
          <button type="button" className="btn btn-secondary" onClick={load} disabled={loading}>
            Refresh
          </button>
        </div>
      </div>

      <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
        <SummaryCard label="Pending" value={summary.pending} tone="pending" />
        <SummaryCard label="Processing" value={summary.processing} tone="processing" />
        <SummaryCard label="Processed" value={summary.processed} tone="processed" />
        <SummaryCard label="Failed" value={summary.failed} tone="failed" />
        <SummaryCard label="Total" value={summary.total} tone="total" />
      </div>

      <div style={{
        display: 'flex',
        gap: '0.75rem',
        flexWrap: 'wrap',
        marginBottom: '1rem',
        alignItems: 'center',
      }}>
        <select
          value={status}
          onChange={(e) => { setStatus(e.target.value); setPage(1); }}
          style={selectStyle}
        >
          {STATUS_OPTIONS.map((o) => (
            <option key={o.value || 'all'} value={o.value}>{o.label}</option>
          ))}
        </select>
        <input
          type="text"
          placeholder="Filter by type / key…"
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && (setPage(1), load())}
          style={{ ...selectStyle, minWidth: '220px' }}
        />
        <button type="button" className="btn btn-secondary" onClick={() => { setPage(1); load(); }}>
          Apply
        </button>
      </div>

      <div style={{
        background: '#1e293b',
        borderRadius: '12px',
        border: '1px solid rgba(255,255,255,0.06)',
        overflow: 'hidden',
      }}>
        {loading ? (
          <div style={{ padding: '3rem', textAlign: 'center' }}><div className="spinner" /></div>
        ) : items.length === 0 ? (
          <div style={{ padding: '2.5rem', textAlign: 'center', color: '#94a3b8' }}>
            No outbox messages match this filter.
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid rgba(255,255,255,0.08)', color: '#94a3b8', textAlign: 'left' }}>
                  <th style={thStyle}>Type</th>
                  <th style={thStyle}>Status</th>
                  <th style={thStyle}>Attempts</th>
                  <th style={thStyle}>Created</th>
                  <th style={thStyle}>Error</th>
                  <th style={thStyle}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {items.map((row) => (
                  <tr key={row.id} style={{ borderBottom: '1px solid rgba(255,255,255,0.04)' }}>
                    <td style={tdStyle}>
                      <div style={{ color: '#e2e8f0', fontWeight: 600 }}>{row.shortTypeName}</div>
                      <div style={{ color: '#64748b', fontSize: '0.75rem', maxWidth: '220px', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {row.idempotencyKey}
                      </div>
                    </td>
                    <td style={tdStyle}><StatusBadge status={row.status} /></td>
                    <td style={tdStyle}><span style={{ color: '#cbd5e1' }}>{row.attemptCount}</span></td>
                    <td style={tdStyle}>
                      <span style={{ color: '#94a3b8' }}>{new Date(row.createdAtUtc).toLocaleString()}</span>
                    </td>
                    <td style={tdStyle}>
                      <span style={{ color: row.lastError ? '#f87171' : '#64748b', maxWidth: '240px', display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={row.lastError || ''}>
                        {row.lastError || '—'}
                      </span>
                    </td>
                    <td style={tdStyle}>
                      <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap' }}>
                        <button type="button" className="btn btn-secondary" style={{ padding: '0.3rem 0.65rem', fontSize: '0.8rem' }} onClick={() => openDetail(row.id)}>
                          Detail
                        </button>
                        {row.status !== 2 && (
                          <button
                            type="button"
                            className="btn btn-primary"
                            style={{ padding: '0.3rem 0.65rem', fontSize: '0.8rem' }}
                            disabled={busyId === row.id}
                            onClick={() => handleRequeue(row.id)}
                          >
                            Requeue
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {totalPages > 1 && (
        <div style={{ display: 'flex', justifyContent: 'center', gap: '0.75rem', marginTop: '1.25rem', alignItems: 'center' }}>
          <button type="button" className="btn btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</button>
          <span style={{ color: '#94a3b8', fontSize: '0.9rem' }}>Page {page} / {totalPages}</span>
          <button type="button" className="btn btn-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      )}

      {selected && (
        <div
          role="dialog"
          style={{
            position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.65)', zIndex: 9000,
            display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem',
          }}
          onClick={() => setSelected(null)}
        >
          <div
            style={{
              background: '#0f172a', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '14px',
              maxWidth: '640px', width: '100%', maxHeight: '85vh', overflow: 'auto', padding: '1.5rem',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
              <h2 style={{ margin: 0, color: 'white', fontSize: '1.15rem' }}>{selected.shortTypeName}</h2>
              <button type="button" className="btn btn-secondary" onClick={() => setSelected(null)}>Close</button>
            </div>
            <div style={{ display: 'grid', gap: '0.65rem', color: '#cbd5e1', fontSize: '0.875rem' }}>
              <div><strong style={{ color: '#94a3b8' }}>Id:</strong> {selected.id}</div>
              <div><strong style={{ color: '#94a3b8' }}>Status:</strong> <StatusBadge status={selected.status} /></div>
              <div><strong style={{ color: '#94a3b8' }}>Idempotency:</strong> {selected.idempotencyKey}</div>
              <div><strong style={{ color: '#94a3b8' }}>Attempts:</strong> {selected.attemptCount}</div>
              {selected.lastError && (
                <div><strong style={{ color: '#94a3b8' }}>Error:</strong> <span style={{ color: '#f87171' }}>{selected.lastError}</span></div>
              )}
              <div>
                <strong style={{ color: '#94a3b8' }}>Payload:</strong>
                <pre style={{
                  marginTop: '0.5rem', background: '#1e293b', padding: '0.85rem', borderRadius: '8px',
                  overflow: 'auto', fontSize: '0.78rem', color: '#e2e8f0',
                }}>
                  {formatJson(selected.payloadPreview)}
                </pre>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function formatJson(text) {
  if (!text) return '';
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

const selectStyle = {
  background: '#1e293b',
  border: '1px solid rgba(255,255,255,0.1)',
  borderRadius: '8px',
  color: '#e2e8f0',
  padding: '0.5rem 0.75rem',
  fontSize: '0.875rem',
};

const thStyle = { padding: '0.75rem 1rem', fontWeight: 600 };
const tdStyle = { padding: '0.85rem 1rem', verticalAlign: 'middle' };
