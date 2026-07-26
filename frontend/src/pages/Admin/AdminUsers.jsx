import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';

function roleLabel(role) {
  if (role === 0 || role === 'Admin') return 'Admin';
  return 'User';
}

export default function AdminUsers() {
  const [search, setSearch] = useState('');
  const [isActive, setIsActive] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminUsers({
        search: search || undefined,
        isActive: isActive === '' ? undefined : isActive === 'true',
        page,
        pageSize: 25,
      });
      if (res?.success) setData(res.data);
      else toast.error(res?.message || 'Failed to load users');
    } catch (e) {
      toast.error(e.message || 'Failed to load users');
    } finally {
      setLoading(false);
    }
  }, [search, isActive, page]);

  useEffect(() => {
    load();
  }, [load]);

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div>
      <header style={{ marginBottom: '1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>Users</h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
          Search accounts and activate or deactivate users for trust & safety.
        </p>
      </header>

      <div style={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: '0.75rem',
        marginBottom: '1rem',
        alignItems: 'center',
      }}>
        <input
          type="search"
          placeholder="Search name, email, phone…"
          value={search}
          onChange={(e) => { setPage(1); setSearch(e.target.value); }}
          style={{
            flex: '1 1 220px',
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            borderRadius: '10px',
            padding: '0.6rem 0.85rem',
            color: 'var(--color-text-primary)',
          }}
        />
        <select
          value={isActive}
          onChange={(e) => { setPage(1); setIsActive(e.target.value); }}
          style={{
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            borderRadius: '10px',
            padding: '0.6rem 0.85rem',
            color: 'var(--color-text-primary)',
          }}
        >
          <option value="">All statuses</option>
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
        <button type="button" className="btn btn-secondary" onClick={load}>Refresh</button>
      </div>

      <div style={{
        background: 'var(--color-surface)',
        borderRadius: '14px',
        border: '1px solid var(--color-border)',
        overflow: 'hidden',
      }}>
        {loading ? (
          <div style={{ padding: '2.5rem', textAlign: 'center' }}><div className="spinner" /></div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
            <thead>
              <tr style={{ background: 'rgba(0,0,0,0.2)', color: 'var(--color-text-secondary)', textAlign: 'left' }}>
                <th style={{ padding: '0.75rem 1rem' }}>User</th>
                <th style={{ padding: '0.75rem 1rem' }}>Role</th>
                <th style={{ padding: '0.75rem 1rem' }}>Status</th>
                <th style={{ padding: '0.75rem 1rem' }}>Last login</th>
                <th style={{ padding: '0.75rem 1rem' }} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ padding: '1.5rem', color: 'var(--color-text-muted)', textAlign: 'center' }}>
                    No users match your filters.
                  </td>
                </tr>
              )}
              {items.map((u) => (
                <tr key={u.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                  <td style={{ padding: '0.8rem 1rem' }}>
                    <div style={{ fontWeight: 600 }}>{u.firstName} {u.lastName}</div>
                    <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.8rem' }}>{u.email}</div>
                  </td>
                  <td style={{ padding: '0.8rem 1rem' }}>{roleLabel(u.role)}</td>
                  <td style={{ padding: '0.8rem 1rem' }}>
                    <span style={{
                      color: u.isActive ? 'var(--color-success)' : 'var(--color-error)',
                      fontWeight: 600,
                    }}>
                      {u.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td style={{ padding: '0.8rem 1rem', color: 'var(--color-text-secondary)' }}>
                    {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : '—'}
                  </td>
                  <td style={{ padding: '0.8rem 1rem', textAlign: 'right' }}>
                    <Link
                      to={`/admin/users/${u.id}`}
                      style={{ color: 'var(--color-accent-light)', textDecoration: 'none', fontWeight: 600 }}
                    >
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
        <span>
          {data ? `${data.totalCount} total` : ''}
        </span>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Previous
          </button>
          <span style={{ alignSelf: 'center' }}>Page {page} / {totalPages || 1}</span>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </button>
        </div>
      </div>
    </div>
  );
}
