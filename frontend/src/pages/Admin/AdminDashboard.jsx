import { useEffect, useState } from 'react';
import api from '../../services/api';
import toast from 'react-hot-toast';

function StatCard({ label, value, hint, accent = 'var(--color-accent-light)' }) {
  return (
    <div style={{
      background: 'var(--color-surface)',
      border: '1px solid var(--color-border)',
      borderRadius: '14px',
      padding: '1.15rem 1.25rem',
      minWidth: 0,
    }}>
      <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.8rem', marginBottom: '0.4rem' }}>{label}</div>
      <div style={{ color: accent, fontSize: '1.65rem', fontWeight: 700, lineHeight: 1.1 }}>
        {value}
      </div>
      {hint && (
        <div style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem', marginTop: '0.45rem' }}>{hint}</div>
      )}
    </div>
  );
}

function formatMoney(n) {
  if (n == null || Number.isNaN(Number(n))) return '—';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 0,
  }).format(Number(n));
}

export default function AdminDashboard() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const res = await api.getAdminDashboard();
        if (!cancelled && res?.success) setData(res.data);
        else if (!cancelled) toast.error(res?.message || 'Failed to load dashboard');
      } catch (e) {
        if (!cancelled) toast.error(e.message || 'Failed to load dashboard');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '4rem' }}>
        <div className="spinner" />
      </div>
    );
  }

  if (!data) {
    return <p style={{ color: 'var(--color-text-secondary)' }}>No dashboard data available.</p>;
  }

  return (
    <div>
      <header style={{ marginBottom: '1.5rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>Platform dashboard</h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
          Snapshot of marketplace health for operators. Updated{' '}
          {data.generatedAtUtc ? new Date(data.generatedAtUtc).toLocaleString() : 'just now'}.
        </p>
      </header>

      <section style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))',
        gap: '0.9rem',
        marginBottom: '1.25rem',
      }}>
        <StatCard label="Total users" value={data.totalUsers} hint={`${data.activeUsers} active`} accent="var(--color-accent-light)" />
        <StatCard label="Admins" value={data.adminUsers} accent="var(--color-secondary)" />
        <StatCard label="Listings" value={data.totalListings} hint={`${data.activeListings} active`} accent="var(--color-success)" />
        <StatCard label="Bookings" value={data.totalBookings} hint={`${data.activeBookings} in flight · ${data.completedBookings} done`} accent="var(--color-warning)" />
        <StatCard label="Payment volume" value={formatMoney(data.totalPaymentVolume)} hint={`${data.totalPayments} payments`} accent="var(--color-accent-light)" />
        <StatCard label="Refunded" value={formatMoney(data.refundedVolume)} accent="var(--color-error)" />
        <StatCard label="Companies" value={data.companies} accent="var(--color-secondary)" />
        <StatCard label="Audit (7d)" value={data.auditEventsLast7Days} accent="var(--color-text-secondary)" />
      </section>
    </div>
  );
}
