import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import api from '../../services/api';

export default function AdminAuditLog() {
  const [action, setAction] = useState('');
  const [resourceType, setResourceType] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminAuditLogs({
        action: action || undefined,
        resourceType: resourceType || undefined,
        page,
        pageSize: 25,
      });
      if (res?.success) setData(res.data);
      else toast.error(res?.message || 'Failed to load audit log');
    } catch (e) {
      toast.error(e.message || 'Failed to load audit log');
    } finally {
      setLoading(false);
    }
  }, [action, resourceType, page]);

  useEffect(() => {
    load();
  }, [load]);

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div>
      <header style={{ marginBottom: '1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>Audit log</h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
          Append-only record of platform admin mutations.
        </p>
      </header>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', marginBottom: '1rem' }}>
        <input
          type="search"
          placeholder="Filter action (e.g. User.Deactivate)"
          value={action}
          onChange={(e) => { setPage(1); setAction(e.target.value); }}
          style={{
            flex: '1 1 200px',
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            borderRadius: '10px',
            padding: '0.6rem 0.85rem',
            color: 'var(--color-text-primary)',
          }}
        />
        <input
          type="search"
          placeholder="Resource type (e.g. User)"
          value={resourceType}
          onChange={(e) => { setPage(1); setResourceType(e.target.value); }}
          style={{
            flex: '0 1 160px',
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            borderRadius: '10px',
            padding: '0.6rem 0.85rem',
            color: 'var(--color-text-primary)',
          }}
        />
        <button type="button" className="btn btn-secondary" onClick={load}>Refresh</button>
      </div>

      <div style={{
        background: 'var(--color-surface)',
        borderRadius: '14px',
        border: '1px solid var(--color-border)',
        overflow: 'auto',
      }}>
        {loading ? (
          <div style={{ padding: '2.5rem', textAlign: 'center' }}><div className="spinner" /></div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem', minWidth: 720 }}>
            <thead>
              <tr style={{ background: 'rgba(0,0,0,0.2)', color: 'var(--color-text-secondary)', textAlign: 'left' }}>
                <th style={{ padding: '0.75rem 1rem' }}>When (UTC)</th>
                <th style={{ padding: '0.75rem 1rem' }}>Actor</th>
                <th style={{ padding: '0.75rem 1rem' }}>Action</th>
                <th style={{ padding: '0.75rem 1rem' }}>Resource</th>
                <th style={{ padding: '0.75rem 1rem' }}>Payload</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ padding: '1.5rem', color: 'var(--color-text-muted)', textAlign: 'center' }}>
                    No audit events yet. Actions like activate/deactivate will appear here.
                  </td>
                </tr>
              )}
              {items.map((row) => (
                <tr key={row.id} style={{ borderTop: '1px solid var(--color-border)', verticalAlign: 'top' }}>
                  <td style={{ padding: '0.75rem 1rem', whiteSpace: 'nowrap', color: 'var(--color-text-secondary)' }}>
                    {row.occurredAtUtc ? new Date(row.occurredAtUtc).toLocaleString() : '—'}
                  </td>
                  <td style={{ padding: '0.75rem 1rem' }}>
                    <div>{row.actorEmail}</div>
                    <div style={{ fontSize: '0.72rem', color: 'var(--color-text-muted)' }}>{row.actorUserId}</div>
                  </td>
                  <td style={{ padding: '0.75rem 1rem', fontWeight: 600, color: 'var(--color-secondary)' }}>{row.action}</td>
                  <td style={{ padding: '0.75rem 1rem' }}>
                    {row.resourceType}
                    {row.resourceId ? (
                      <div style={{ fontSize: '0.72rem', color: 'var(--color-text-muted)' }}>{row.resourceId}</div>
                    ) : null}
                  </td>
                  <td style={{ padding: '0.75rem 1rem', maxWidth: 280 }}>
                    <code style={{
                      display: 'block',
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-word',
                      fontSize: '0.72rem',
                      color: 'var(--color-text-secondary)',
                    }}>
                      {row.payloadJson || '—'}
                    </code>
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
        marginTop: '1rem',
        color: 'var(--color-text-secondary)',
        fontSize: '0.85rem',
      }}>
        <span>{data ? `${data.totalCount} events` : ''}</span>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button type="button" className="btn btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Previous
          </button>
          <span style={{ alignSelf: 'center' }}>Page {page} / {totalPages || 1}</span>
          <button type="button" className="btn btn-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            Next
          </button>
        </div>
      </div>
    </div>
  );
}
