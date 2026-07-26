import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';

export default function AdminListings() {
  const [search, setSearch] = useState('');
  const [isActive, setIsActive] = useState('');
  const [isVerified, setIsVerified] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminListings({
        search: search || undefined,
        isActive: isActive === '' ? undefined : isActive === 'true',
        isVerified: isVerified === '' ? undefined : isVerified === 'true',
        page,
        pageSize: 25,
      });
      if (res?.success) setData(res.data);
      else toast.error(res?.message || 'Failed to load listings');
    } catch (e) {
      toast.error(e.message || 'Failed to load listings');
    } finally {
      setLoading(false);
    }
  }, [search, isActive, isVerified, page]);

  useEffect(() => {
    load();
  }, [load]);

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div>
      <header style={{ marginBottom: '1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>Listings</h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
          Moderate parking spaces — verify, activate, or take offline.
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
          placeholder="Search title, city, address…"
          value={search}
          onChange={(e) => { setPage(1); setSearch(e.target.value); }}
          style={inputStyle}
        />
        <select value={isActive} onChange={(e) => { setPage(1); setIsActive(e.target.value); }} style={selectStyle}>
          <option value="">All active</option>
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
        <select value={isVerified} onChange={(e) => { setPage(1); setIsVerified(e.target.value); }} style={selectStyle}>
          <option value="">All verified</option>
          <option value="true">Verified</option>
          <option value="false">Unverified</option>
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
                <th style={th}>Listing</th>
                <th style={th}>Status</th>
                <th style={th}>Verified</th>
                <th style={th}>Rate/hr</th>
                <th style={th} />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ padding: '1.5rem', color: 'var(--color-text-muted)', textAlign: 'center' }}>
                    No listings match your filters.
                  </td>
                </tr>
              )}
              {items.map((p) => (
                <tr key={p.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                  <td style={td}>
                    <div style={{ fontWeight: 600 }}>{p.title}</div>
                    <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.8rem' }}>
                      {p.city}, {p.state}
                    </div>
                  </td>
                  <td style={td}>
                    <span style={{ color: p.isActive ? 'var(--color-success)' : 'var(--color-error)', fontWeight: 600 }}>
                      {p.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td style={td}>{p.isVerified ? 'Yes' : 'No'}</td>
                  <td style={td}>{p.hourlyRate != null ? Number(p.hourlyRate).toFixed(2) : '—'}</td>
                  <td style={{ ...td, textAlign: 'right' }}>
                    <Link to={`/admin/listings/${p.id}`} style={{ color: 'var(--color-accent-light)', textDecoration: 'none', fontWeight: 600 }}>
                      View
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <Pager page={page} totalPages={totalPages} total={data?.totalCount} setPage={setPage} />
    </div>
  );
}

function Pager({ page, totalPages, total, setPage }) {
  return (
    <div style={{
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      marginTop: '1rem',
      color: 'var(--color-text-secondary)',
      fontSize: '0.85rem',
    }}>
      <span>{total != null ? `${total} total` : ''}</span>
      <div style={{ display: 'flex', gap: '0.5rem' }}>
        <button type="button" className="btn btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
          Previous
        </button>
        <span style={{ alignSelf: 'center' }}>Page {page} / {totalPages || 1}</span>
        <button type="button" className="btn btn-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
          Next
        </button>
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
