import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import api from '../../services/api';

function StatusPill({ children, tone = 'muted' }) {
  const colors = {
    success: { color: 'var(--color-success)', border: 'rgba(34,197,94,0.35)' },
    error: { color: 'var(--color-error)', border: 'rgba(248,113,113,0.35)' },
    warning: { color: 'var(--color-warning)', border: 'rgba(234,179,8,0.35)' },
    muted: { color: 'var(--color-text-secondary)', border: 'var(--color-border)' },
  };
  const c = colors[tone] || colors.muted;
  return (
    <span style={{
      display: 'inline-block',
      fontSize: '0.72rem',
      fontWeight: 700,
      letterSpacing: '0.02em',
      textTransform: 'uppercase',
      padding: '0.2rem 0.5rem',
      borderRadius: '999px',
      border: `1px solid ${c.border}`,
      color: c.color,
      whiteSpace: 'nowrap',
    }}>
      {children}
    </span>
  );
}

function effectiveTone(row) {
  if (row.forceDisabledByPlatform) return 'error';
  if (row.isEffectivelyEnabled) return 'success';
  if (row.isEnabled) return 'warning';
  return 'muted';
}

function effectiveLabel(row) {
  if (row.forceDisabledByPlatform) return 'Force-disabled';
  if (row.isEffectivelyEnabled) return 'Live';
  if (row.isEnabled) return 'Enabled (not effective)';
  return 'Disabled';
}

/**
 * Platform Admin — Corporate SSO oversight (PR6 API + PR7 admin UI).
 * List tenants, force-disable / clear lock, inspect SSO audit.
 */
export default function AdminCorporateSso() {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState(''); // '', force, enabled
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);

  const [selected, setSelected] = useState(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const [auditOpen, setAuditOpen] = useState(false);
  const [auditLoading, setAuditLoading] = useState(false);
  const [auditEvents, setAuditEvents] = useState([]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminCorporateSsoList({
        search: search || undefined,
        forceDisabledOnly: statusFilter === 'force' ? true : undefined,
        enabledOnly: statusFilter === 'enabled' ? true : undefined,
        page,
        pageSize: 25,
      });
      if (res?.success) setData(res.data);
      else toast.error(res?.message || 'Failed to load Corporate SSO list');
    } catch (e) {
      toast.error(e.message || 'Failed to load Corporate SSO list');
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, page]);

  useEffect(() => {
    load();
  }, [load]);

  // Keep selection in sync after reload
  useEffect(() => {
    if (!selected || !data?.items) return;
    const next = data.items.find((r) => r.companyId === selected.companyId);
    if (next) setSelected(next);
  }, [data, selected?.companyId]); // eslint-disable-line react-hooks/exhaustive-deps

  const items = data?.items ?? [];
  const totalPages = data
    ? Math.max(1, Math.ceil((data.totalCount || 0) / (data.pageSize || 25)))
    : 1;

  const openAudit = async (row) => {
    setSelected(row);
    setAuditOpen(true);
    setAuditLoading(true);
    setAuditEvents([]);
    try {
      const res = await api.getAdminCompanySsoAudit(row.companyId, 50);
      if (res?.success) setAuditEvents(res.data || []);
      else toast.error(res?.message || 'Failed to load SSO audit');
    } catch (e) {
      toast.error(e.message || 'Failed to load SSO audit');
    } finally {
      setAuditLoading(false);
    }
  };

  const runForceDisable = async () => {
    if (!selected) return;
    if (!reason.trim()) {
      toast.error('Reason is required for force-disable (incident audit)');
      return;
    }
    if (!window.confirm(
      `Force-disable SSO for "${selected.companyName}"?\n\nCompany admins cannot re-enable until you clear the platform lock.`,
    )) {
      return;
    }
    setBusy(true);
    try {
      const res = await api.forceDisableCompanySso(selected.companyId, reason.trim());
      if (res?.success) {
        toast.success(res.message || 'SSO force-disabled');
        setReason('');
        await load();
      } else {
        toast.error(res?.message || 'Force-disable failed');
      }
    } catch (e) {
      toast.error(e.message || 'Force-disable failed');
    } finally {
      setBusy(false);
    }
  };

  const runClearForceDisable = async () => {
    if (!selected) return;
    if (!window.confirm(
      `Clear platform force-disable for "${selected.companyName}"?\n\nSSO stays disabled until the company admin re-enables it.`,
    )) {
      return;
    }
    setBusy(true);
    try {
      const res = await api.clearForceDisableCompanySso(
        selected.companyId,
        reason.trim() || undefined,
      );
      if (res?.success) {
        toast.success(res.message || 'Force-disable cleared');
        setReason('');
        await load();
      } else {
        toast.error(res?.message || 'Clear force-disable failed');
      }
    } catch (e) {
      toast.error(e.message || 'Clear force-disable failed');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div data-testid="admin-corporate-sso">
      <header style={{ marginBottom: '1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>Corporate SSO</h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
          Platform oversight for enterprise IdP federation. Force-disable is sticky — tenants cannot undo it.
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
          placeholder="Search company name or slug…"
          value={search}
          onChange={(e) => { setPage(1); setSearch(e.target.value); }}
          data-testid="admin-sso-search"
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
          value={statusFilter}
          onChange={(e) => { setPage(1); setStatusFilter(e.target.value); }}
          data-testid="admin-sso-filter"
          style={{
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            borderRadius: '10px',
            padding: '0.6rem 0.85rem',
            color: 'var(--color-text-primary)',
          }}
        >
          <option value="">All with SSO config</option>
          <option value="enabled">Tenant-enabled only</option>
          <option value="force">Force-disabled only</option>
        </select>
        <button type="button" className="btn btn-secondary" onClick={load}>Refresh</button>
      </div>

      <div style={{
        display: 'grid',
        gridTemplateColumns: selected ? 'minmax(0, 1fr) 320px' : '1fr',
        gap: '1rem',
        alignItems: 'start',
      }}>
        <div style={{
          background: 'var(--color-surface)',
          borderRadius: '14px',
          border: '1px solid var(--color-border)',
          overflow: 'auto',
        }}>
          {loading ? (
            <div style={{ padding: '2.5rem', textAlign: 'center' }}><div className="spinner" /></div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem', minWidth: 720 }}>
              <thead>
                <tr style={{ background: 'var(--color-table-head)', color: 'var(--color-text-secondary)', textAlign: 'left' }}>
                  <th style={{ padding: '0.75rem 1rem' }}>Company</th>
                  <th style={{ padding: '0.75rem 1rem' }}>Status</th>
                  <th style={{ padding: '0.75rem 1rem' }}>IdP</th>
                  <th style={{ padding: '0.75rem 1rem' }}>Domains</th>
                  <th style={{ padding: '0.75rem 1rem' }}>SSO logins (7d)</th>
                  <th style={{ padding: '0.75rem 1rem' }} />
                </tr>
              </thead>
              <tbody>
                {items.length === 0 && (
                  <tr>
                    <td colSpan={6} style={{ padding: '1.5rem', color: 'var(--color-text-muted)', textAlign: 'center' }}>
                      No companies with SSO configuration match your filters.
                    </td>
                  </tr>
                )}
                {items.map((row) => {
                  const isSelected = selected?.companyId === row.companyId;
                  return (
                    <tr
                      key={row.companyId}
                      style={{
                        borderTop: '1px solid var(--color-border)',
                        background: isSelected ? 'var(--color-primary-alpha)' : 'transparent',
                        cursor: 'pointer',
                      }}
                      onClick={() => setSelected(row)}
                      data-testid={`admin-sso-row-${row.companyId}`}
                    >
                      <td style={{ padding: '0.8rem 1rem' }}>
                        <div style={{ fontWeight: 600 }}>{row.companyName}</div>
                        <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.78rem' }}>
                          {row.companySlug ? `/${row.companySlug}` : row.companyId}
                          {!row.companyIsActive && (
                            <span style={{ marginLeft: 8, color: 'var(--color-error)' }}>inactive</span>
                          )}
                        </div>
                      </td>
                      <td style={{ padding: '0.8rem 1rem' }}>
                        <StatusPill tone={effectiveTone(row)}>{effectiveLabel(row)}</StatusPill>
                        {row.forceDisabledAt && (
                          <div style={{ fontSize: '0.72rem', color: 'var(--color-text-muted)', marginTop: 4 }}>
                            Locked {new Date(row.forceDisabledAt).toLocaleString()}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '0.8rem 1rem' }}>
                        <div style={{ fontWeight: 500 }}>{row.protocol || 'OIDC'}</div>
                        <div style={{
                          color: 'var(--color-text-secondary)',
                          fontSize: '0.75rem',
                          maxWidth: 180,
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                        }} title={row.authority || ''}>
                          {row.authority || '—'}
                        </div>
                        {row.lastTestedAt && (
                          <div style={{ fontSize: '0.72rem', color: 'var(--color-text-muted)', marginTop: 2 }}>
                            Test: {row.lastTestResult || '—'} · {new Date(row.lastTestedAt).toLocaleDateString()}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '0.8rem 1rem' }}>{row.verifiedDomainCount ?? 0}</td>
                      <td style={{ padding: '0.8rem 1rem' }}>{row.ssoLoginSuccessCount7d ?? 0}</td>
                      <td style={{ padding: '0.8rem 1rem', textAlign: 'right' }}>
                        <button
                          type="button"
                          className="btn btn-secondary"
                          style={{ fontSize: '0.8rem', padding: '0.35rem 0.65rem' }}
                          onClick={(e) => { e.stopPropagation(); openAudit(row); }}
                          data-testid={`admin-sso-audit-${row.companyId}`}
                        >
                          Audit
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {selected && (
          <aside style={{
            background: 'var(--color-surface)',
            borderRadius: '14px',
            border: '1px solid var(--color-border)',
            padding: '1.15rem 1.2rem',
            position: 'sticky',
            top: '0.5rem',
          }} data-testid="admin-sso-detail">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 700 }}>{selected.companyName}</h2>
                <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', marginTop: 4 }}>
                  {selected.companyId}
                </div>
              </div>
              <button
                type="button"
                className="btn btn-secondary"
                style={{ fontSize: '0.75rem', padding: '0.3rem 0.55rem' }}
                onClick={() => { setSelected(null); setAuditOpen(false); }}
              >
                Close
              </button>
            </div>

            <div style={{ marginTop: '0.9rem', display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              <StatusPill tone={effectiveTone(selected)}>{effectiveLabel(selected)}</StatusPill>
              {selected.isEnabled && !selected.forceDisabledByPlatform && (
                <StatusPill tone="success">IsEnabled</StatusPill>
              )}
            </div>

            <dl style={{
              margin: '1rem 0 0',
              fontSize: '0.82rem',
              display: 'grid',
              gap: '0.55rem',
              color: 'var(--color-text-secondary)',
            }}>
              <div>
                <dt style={{ fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: '0.04em' }}>Slug</dt>
                <dd style={{ margin: '0.15rem 0 0', color: 'var(--color-text-primary)' }}>
                  {selected.companySlug || '—'}
                </dd>
              </div>
              <div>
                <dt style={{ fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: '0.04em' }}>Authority</dt>
                <dd style={{ margin: '0.15rem 0 0', color: 'var(--color-text-primary)', wordBreak: 'break-all' }}>
                  {selected.authority || '—'}
                </dd>
              </div>
              <div>
                <dt style={{ fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: '0.04em' }}>Verified domains</dt>
                <dd style={{ margin: '0.15rem 0 0', color: 'var(--color-text-primary)' }}>
                  {selected.verifiedDomainCount ?? 0}
                </dd>
              </div>
              <div>
                <dt style={{ fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: '0.04em' }}>SSO success (7d)</dt>
                <dd style={{ margin: '0.15rem 0 0', color: 'var(--color-text-primary)' }}>
                  {selected.ssoLoginSuccessCount7d ?? 0}
                </dd>
              </div>
              {selected.forceDisabledByUserId && (
                <div>
                  <dt style={{ fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: '0.04em' }}>Locked by</dt>
                  <dd style={{ margin: '0.15rem 0 0', color: 'var(--color-text-primary)', fontSize: '0.75rem', wordBreak: 'break-all' }}>
                    {selected.forceDisabledByUserId}
                    {selected.forceDisabledAt && (
                      <div style={{ color: 'var(--color-text-muted)' }}>
                        {new Date(selected.forceDisabledAt).toLocaleString()}
                      </div>
                    )}
                  </dd>
                </div>
              )}
            </dl>

            <div style={{
              marginTop: '1.1rem',
              paddingTop: '1rem',
              borderTop: '1px solid var(--color-border)',
            }}>
              <label style={{
                display: 'block',
                fontSize: '0.8rem',
                fontWeight: 600,
                marginBottom: '0.4rem',
                color: 'var(--color-text-primary)',
              }}>
                Reason (audited)
              </label>
              <textarea
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                rows={3}
                placeholder="Incident ticket, abuse report, customer request…"
                data-testid="admin-sso-reason"
                style={{
                  width: '100%',
                  boxSizing: 'border-box',
                  background: 'var(--color-bg-primary)',
                  border: '1px solid var(--color-border)',
                  borderRadius: '10px',
                  padding: '0.55rem 0.7rem',
                  color: 'var(--color-text-primary)',
                  fontSize: '0.85rem',
                  resize: 'vertical',
                }}
              />

              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginTop: '0.75rem' }}>
                {!selected.forceDisabledByPlatform ? (
                  <button
                    type="button"
                    className="btn btn-primary"
                    disabled={busy}
                    onClick={runForceDisable}
                    data-testid="admin-sso-force-disable"
                    style={{
                      background: 'var(--color-error)',
                      borderColor: 'var(--color-error)',
                    }}
                  >
                    {busy ? 'Working…' : 'Force-disable SSO'}
                  </button>
                ) : (
                  <button
                    type="button"
                    className="btn btn-primary"
                    disabled={busy}
                    onClick={runClearForceDisable}
                    data-testid="admin-sso-clear-force-disable"
                  >
                    {busy ? 'Working…' : 'Clear force-disable'}
                  </button>
                )}
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => openAudit(selected)}
                  data-testid="admin-sso-open-audit"
                >
                  View SSO audit
                </button>
              </div>

              <p style={{
                margin: '0.85rem 0 0',
                fontSize: '0.75rem',
                color: 'var(--color-text-muted)',
                lineHeight: 1.45,
              }}>
                Clear force-disable does <strong>not</strong> re-enable SSO. Company admin must enable again
                after verifying IdP health. Password break-glass for SSO-only tenants: clear lock and/or
                temporarily restore password login via support process.
              </p>
            </div>
          </aside>
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
        <span>{data ? `${data.totalCount} companies` : ''}</span>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Previous
          </button>
          <span style={{ alignSelf: 'center' }}>Page {page} / {totalPages}</span>
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

      {auditOpen && (
        <div style={{
          marginTop: '1.25rem',
          background: 'var(--color-surface)',
          borderRadius: '14px',
          border: '1px solid var(--color-border)',
          padding: '1.15rem 1.25rem',
        }} data-testid="admin-sso-audit-panel">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
            <h3 style={{ margin: 0, fontSize: '1rem' }}>
              SSO audit{selected ? ` — ${selected.companyName}` : ''}
            </h3>
            <button type="button" className="btn btn-secondary" style={{ fontSize: '0.8rem' }} onClick={() => setAuditOpen(false)}>
              Close
            </button>
          </div>
          {auditLoading ? (
            <div style={{ padding: '1.5rem', textAlign: 'center' }}><div className="spinner" /></div>
          ) : auditEvents.length === 0 ? (
            <p style={{ color: 'var(--color-text-muted)', margin: 0 }}>No audit events yet.</p>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                <thead>
                  <tr style={{ color: 'var(--color-text-secondary)', textAlign: 'left' }}>
                    <th style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>When</th>
                    <th style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>Action</th>
                    <th style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>Outcome</th>
                    <th style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>Error</th>
                    <th style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>Detail</th>
                  </tr>
                </thead>
                <tbody>
                  {auditEvents.map((ev) => (
                    <tr key={ev.id}>
                      <td style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)', color: 'var(--color-text-secondary)', whiteSpace: 'nowrap' }}>
                        {ev.createdAt ? new Date(ev.createdAt).toLocaleString() : '—'}
                      </td>
                      <td style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>{ev.action}</td>
                      <td style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)' }}>{ev.outcome}</td>
                      <td style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)', color: 'var(--color-text-muted)' }}>
                        {ev.errorCode || '—'}
                      </td>
                      <td style={{ padding: '0.5rem', borderBottom: '1px solid var(--color-border)', maxWidth: 280 }}>
                        <code style={{
                          display: 'block',
                          fontSize: '0.72rem',
                          color: 'var(--color-text-secondary)',
                          whiteSpace: 'pre-wrap',
                          wordBreak: 'break-word',
                        }}>
                          {ev.detailJson || '—'}
                        </code>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
