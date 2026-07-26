import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import api from '../../services/api';

function statusLabel(s) {
  const map = { 0: 'Pending', 1: 'Completed', 2: 'Failed', 3: 'Refunded', 4: 'PartialRefund' };
  const n = typeof s === 'number' ? s : Number(s);
  return map[n] || String(s);
}

export default function AdminPaymentDetail() {
  const { id } = useParams();
  const [payment, setPayment] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reason, setReason] = useState('');
  const [amount, setAmount] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getAdminPayment(id);
      if (res?.success) setPayment(res.data);
      else toast.error(res?.message || 'Payment not found');
    } catch (e) {
      toast.error(e.message || 'Failed to load payment');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const refund = async () => {
    if (!reason.trim()) {
      toast.error('Reason is required');
      return;
    }
    const parsed = amount.trim() === '' ? null : Number(amount);
    if (parsed != null && (Number.isNaN(parsed) || parsed <= 0)) {
      toast.error('Amount must be a positive number, or leave blank for full remaining');
      return;
    }
    if (!window.confirm('Process this refund via the payment gateway?')) return;
    setBusy(true);
    try {
      const res = await api.refundAdminPayment(id, reason.trim(), parsed);
      if (res?.success) {
        setPayment(res.data);
        setReason('');
        setAmount('');
        toast.success(res.message || 'Refund processed');
      } else {
        toast.error(res?.message || 'Refund failed');
      }
    } catch (e) {
      toast.error(e.message || 'Refund failed');
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

  if (!payment) {
    return (
      <div>
        <Link to="/admin/payments" style={{ color: 'var(--color-accent-light)' }}>← Back to payments</Link>
        <p style={{ color: 'var(--color-text-secondary)', marginTop: '1rem' }}>Payment not found.</p>
      </div>
    );
  }

  const canRefund =
    (payment.status === 1 || payment.status === 4 || payment.status === 'Completed' || payment.status === 'PartialRefund')
    && (payment.remainingRefundable ?? 0) > 0;

  return (
    <div>
      <Link to="/admin/payments" style={{ color: 'var(--color-accent-light)', fontSize: '0.9rem', textDecoration: 'none' }}>
        ← Back to payments
      </Link>
      <header style={{ margin: '0.75rem 0 1.25rem' }}>
        <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>
          Payment {payment.id?.slice(0, 8)}…
        </h1>
        <p style={{ margin: '0.35rem 0 0', color: 'var(--color-text-secondary)' }}>
          {statusLabel(payment.status)} · {Number(payment.amount).toFixed(2)} {payment.currency}
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
          <Row label="Booking" value={<Link to={`/admin/bookings/${payment.bookingId}`} style={{ color: 'var(--color-accent-light)' }}>{payment.bookingId?.slice(0, 8)}…</Link>} />
          <Row label="User" value={<Link to={`/admin/users/${payment.userId}`} style={{ color: 'var(--color-accent-light)' }}>{payment.userId?.slice(0, 8)}…</Link>} />
          <Row label="Gateway txn" value={payment.transactionId || '—'} />
          <Row label="Gateway" value={payment.paymentGateway || '—'} />
          <Row label="Invoice" value={payment.invoiceNumber || '—'} />
          <Row label="Refunded" value={payment.refundAmount != null ? Number(payment.refundAmount).toFixed(2) : '—'} />
          <Row label="Remaining" value={payment.remainingRefundable != null ? Number(payment.remainingRefundable).toFixed(2) : '—'} />
          <Row label="Paid at" value={payment.paidAt ? new Date(payment.paidAt).toLocaleString() : '—'} />
          {payment.refundReason && <Row label="Last refund reason" value={payment.refundReason} />}
        </div>

        {canRefund && (
          <div style={{ ...card, border: '1px solid rgba(248,113,113,0.15)' }}>
            <h2 style={{ ...cardTitle, color: 'var(--color-error)' }}>Admin refund</h2>
            <p style={{ margin: '0 0 0.85rem', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
              Uses the gateway PaymentIntent on this payment. Leave amount blank to refund the full remaining balance
              ({Number(payment.remainingRefundable || 0).toFixed(2)}).
            </p>
            <label style={{ display: 'block', fontSize: '0.8rem', color: 'var(--color-text-secondary)', marginBottom: '0.35rem' }}>
              Amount (optional)
            </label>
            <input
              type="number"
              min="0"
              step="0.01"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="Full remaining if empty"
              style={{
                width: '100%',
                boxSizing: 'border-box',
                background: 'var(--color-bg-primary)',
                border: '1px solid var(--color-border)',
                borderRadius: '10px',
                padding: '0.65rem 0.75rem',
                color: 'var(--color-text-primary)',
                marginBottom: '0.75rem',
              }}
            />
            <label style={{ display: 'block', fontSize: '0.8rem', color: 'var(--color-text-secondary)', marginBottom: '0.35rem' }}>
              Reason
            </label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
              maxLength={500}
              placeholder="e.g. Goodwill / chargeback / double charge"
              style={textareaStyle}
            />
            <button
              type="button"
              className="btn btn-primary"
              disabled={busy}
              onClick={refund}
              style={{ background: 'var(--color-error)', borderColor: 'var(--color-error)' }}
            >
              {busy ? 'Working…' : 'Process refund'}
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
