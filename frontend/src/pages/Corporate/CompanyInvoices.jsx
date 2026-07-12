import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';

const INVOICE_STATUS = ['Draft', 'Issued', 'Paid', 'Void'];
const STATUS_COLORS = {
  0: '#f59e0b',
  1: '#3b82f6',
  2: '#10b981',
  3: '#9ca3af',
};
const BILLING_TYPES = {
  0: 'Reserved Slots',
  1: 'Usage Based',
};
const LINE_TYPES = {
  0: 'Reserved capacity',
  1: 'Usage',
};

const StatusBadge = ({ status }) => (
  <span style={{
    display: 'inline-flex',
    alignItems: 'center',
    minHeight: '26px',
    padding: '3px 10px',
    borderRadius: '999px',
    fontSize: '0.78rem',
    fontWeight: 700,
    whiteSpace: 'nowrap',
    background: `${STATUS_COLORS[status] || '#64748b'}22`,
    color: STATUS_COLORS[status] || '#94a3b8',
    border: `1px solid ${STATUS_COLORS[status] || '#64748b'}55`,
  }}>
    {INVOICE_STATUS[status] ?? `Status ${status}`}
  </span>
);

const formatMoney = (value, currency = 'INR') => {
  const n = Number(value);
  if (Number.isNaN(n)) return '—';
  return n.toLocaleString(undefined, { style: 'currency', currency, maximumFractionDigits: 2 });
};

const toDateInputValue = (d) => {
  if (!d) return '';
  return d.toISOString().slice(0, 10);
};

const previousMonthRange = () => {
  const now = new Date();
  const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 1, 1));
  const end = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 0));
  return { start: toDateInputValue(start), end: toDateInputValue(end) };
};

const CompanyInvoices = () => {
  const { activeCompanyId, companyDetails, isCorporateMode } = useCompany();
  const navigate = useNavigate();
  const defaults = useMemo(() => previousMonthRange(), []);

  const [invoices, setInvoices] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('all');
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [periodStart, setPeriodStart] = useState(defaults.start);
  const [periodEnd, setPeriodEnd] = useState(defaults.end);

  const [detail, setDetail] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [actionBusy, setActionBusy] = useState(false);

  const [payModal, setPayModal] = useState(null);
  const [payRef, setPayRef] = useState('');
  const [payNotes, setPayNotes] = useState('');
  const [voidModal, setVoidModal] = useState(null);
  const [voidReason, setVoidReason] = useState('');

  const loadInvoices = useCallback(async () => {
    if (!activeCompanyId) return;
    setLoading(true);
    try {
      const res = await corporateService.getInvoices(page, 20, statusFilter === 'all' ? null : statusFilter);
      if (res.success && res.data) {
        setInvoices(res.data.items || []);
        setTotalCount(res.data.totalCount || 0);
      } else {
        toast.error(res.message || 'Failed to load invoices');
        setInvoices([]);
        setTotalCount(0);
      }
    } catch (e) {
      toast.error(e.message || 'Failed to load invoices');
    } finally {
      setLoading(false);
    }
  }, [activeCompanyId, page, statusFilter]);

  useEffect(() => {
    if (!isCorporateMode) {
      navigate('/dashboard', { replace: true });
      return;
    }
    loadInvoices();
  }, [isCorporateMode, navigate, loadInvoices]);

  const openDetail = async (invoiceId) => {
    setDetailLoading(true);
    setDetail(null);
    try {
      const res = await corporateService.getInvoice(invoiceId);
      if (res.success && res.data) {
        setDetail(res.data);
      } else {
        toast.error(res.message || 'Failed to load invoice');
      }
    } catch (e) {
      toast.error(e.message || 'Failed to load invoice');
    } finally {
      setDetailLoading(false);
    }
  };

  const handleGenerate = async () => {
    if (!periodStart || !periodEnd) {
      toast.error('Select a billing period');
      return;
    }
    setGenerating(true);
    try {
      const res = await corporateService.generateInvoice({ periodStart, periodEnd });
      if (res.success && res.data) {
        toast.success(res.message || 'Draft invoice generated');
        setPage(1);
        await loadInvoices();
        setDetail(res.data);
        if (Number(res.data.totalAmount) === 0) {
          toast('Invoice total is ₹0 — period closed with no billable lines', { icon: 'ℹ️' });
        }
      } else {
        toast.error(res.message || 'Generate failed');
      }
    } catch (e) {
      toast.error(e.message || 'Generate failed');
    } finally {
      setGenerating(false);
    }
  };

  const runIssue = async (invoiceId) => {
    setActionBusy(true);
    try {
      const res = await corporateService.issueInvoice(invoiceId);
      if (res.success) {
        toast.success('Invoice issued');
        await loadInvoices();
        if (detail?.id === invoiceId) setDetail(res.data);
      } else {
        toast.error(res.message || 'Issue failed');
      }
    } catch (e) {
      toast.error(e.message || 'Issue failed');
    } finally {
      setActionBusy(false);
    }
  };

  const runMarkPaid = async () => {
    if (!payModal) return;
    setActionBusy(true);
    try {
      const res = await corporateService.markInvoicePaid(payModal, {
        paymentReference: payRef || undefined,
        paymentNotes: payNotes || undefined,
      });
      if (res.success) {
        toast.success('Invoice marked as paid');
        setPayModal(null);
        setPayRef('');
        setPayNotes('');
        await loadInvoices();
        if (detail?.id === payModal) setDetail(res.data);
      } else {
        toast.error(res.message || 'Mark paid failed');
      }
    } catch (e) {
      toast.error(e.message || 'Mark paid failed');
    } finally {
      setActionBusy(false);
    }
  };

  const runVoid = async () => {
    if (!voidModal || voidReason.trim().length < 3) {
      toast.error('Void reason must be at least 3 characters');
      return;
    }
    setActionBusy(true);
    try {
      const res = await corporateService.voidInvoice(voidModal, { reason: voidReason.trim() });
      if (res.success) {
        toast.success('Invoice voided');
        setVoidModal(null);
        setVoidReason('');
        await loadInvoices();
        if (detail?.id === voidModal) setDetail(res.data);
      } else {
        toast.error(res.message || 'Void failed');
      }
    } catch (e) {
      toast.error(e.message || 'Void failed');
    } finally {
      setActionBusy(false);
    }
  };

  const runExport = async (invoiceId, invoiceNumber) => {
    try {
      const blob = await corporateService.exportInvoice(invoiceId);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `invoice-${invoiceNumber || invoiceId}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success('Export downloaded');
    } catch (e) {
      toast.error(e.message || 'Export failed');
    }
  };

  if (!isCorporateMode) return null;

  return (
    <div style={{ maxWidth: 1200, margin: '0 auto', padding: '1.5rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', marginBottom: '1.5rem', flexWrap: 'wrap' }}>
        <div>
          <h1 style={{ margin: 0, color: 'white', fontSize: '1.6rem' }}>Corporate Invoices</h1>
          <p style={{ margin: '6px 0 0', color: '#94a3b8', fontSize: '0.9rem' }}>
            {companyDetails?.name || 'Company'} · Manual period billing · Offline mark paid
          </p>
        </div>
        <Link to="/corporate/dashboard" style={{ color: '#94a3b8', fontSize: '0.9rem' }}>← Dashboard</Link>
      </div>

      <div style={{
        background: '#1e293b',
        borderRadius: 12,
        padding: '1.25rem',
        border: '1px solid rgba(255,255,255,0.06)',
        marginBottom: '1.5rem',
      }}>
        <h2 style={{ margin: '0 0 1rem', color: 'white', fontSize: '1.05rem' }}>Generate draft invoice</h2>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '1rem', alignItems: 'flex-end' }}>
          <div>
            <label style={{ display: 'block', color: '#94a3b8', fontSize: '0.8rem', marginBottom: 4 }}>Period start (UTC)</label>
            <input
              type="date"
              value={periodStart}
              onChange={(e) => setPeriodStart(e.target.value)}
              style={{ padding: '8px 10px', background: '#0f172a', border: '1px solid #334155', borderRadius: 6, color: 'white' }}
            />
          </div>
          <div>
            <label style={{ display: 'block', color: '#94a3b8', fontSize: '0.8rem', marginBottom: 4 }}>Period end (UTC)</label>
            <input
              type="date"
              value={periodEnd}
              onChange={(e) => setPeriodEnd(e.target.value)}
              style={{ padding: '8px 10px', background: '#0f172a', border: '1px solid #334155', borderRadius: 6, color: 'white' }}
            />
          </div>
          <button
            type="button"
            className="btn btn-primary"
            disabled={generating}
            onClick={handleGenerate}
          >
            {generating ? 'Generating…' : 'Generate draft'}
          </button>
        </div>
        <p style={{ margin: '0.75rem 0 0', color: '#64748b', fontSize: '0.82rem' }}>
          Lines follow company billing type: Reserved Slots → vendor lease rates (prorated); Usage Based → booking amounts.
          Max 92 days. One non-void invoice per exact period.
        </p>
      </div>

      <div style={{ display: 'flex', gap: '0.75rem', marginBottom: '1rem', flexWrap: 'wrap', alignItems: 'center' }}>
        <label style={{ color: '#94a3b8', fontSize: '0.85rem' }}>Status</label>
        <select
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          style={{ padding: '8px 10px', background: '#0f172a', border: '1px solid #334155', borderRadius: 6, color: 'white' }}
        >
          <option value="all">All</option>
          {INVOICE_STATUS.map((label, i) => (
            <option key={label} value={i}>{label}</option>
          ))}
        </select>
        <span style={{ color: '#64748b', fontSize: '0.85rem' }}>{totalCount} total</span>
      </div>

      <div style={{ background: '#1e293b', borderRadius: 12, border: '1px solid rgba(255,255,255,0.06)', overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: '2rem', textAlign: 'center', color: '#94a3b8' }}>Loading…</div>
        ) : invoices.length === 0 ? (
          <div style={{ padding: '2rem', textAlign: 'center', color: '#94a3b8' }}>
            No invoices yet. Generate a draft for a billing period above.
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
              <thead>
                <tr style={{ color: '#94a3b8', textAlign: 'left', borderBottom: '1px solid #334155' }}>
                  <th style={{ padding: '12px 14px' }}>Invoice</th>
                  <th style={{ padding: '12px 14px' }}>Period</th>
                  <th style={{ padding: '12px 14px' }}>Billing</th>
                  <th style={{ padding: '12px 14px' }}>Status</th>
                  <th style={{ padding: '12px 14px' }}>Total</th>
                  <th style={{ padding: '12px 14px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {invoices.map((inv) => (
                  <tr key={inv.id} style={{ borderBottom: '1px solid #1e293b', background: '#0f172a' }}>
                    <td style={{ padding: '12px 14px', color: 'white', fontWeight: 600 }}>{inv.invoiceNumber}</td>
                    <td style={{ padding: '12px 14px', color: '#cbd5e1' }}>
                      {inv.periodStart} → {inv.periodEnd}
                    </td>
                    <td style={{ padding: '12px 14px', color: '#cbd5e1' }}>
                      {BILLING_TYPES[inv.billingTypeSnapshot] ?? inv.billingTypeSnapshot}
                    </td>
                    <td style={{ padding: '12px 14px' }}><StatusBadge status={inv.status} /></td>
                    <td style={{ padding: '12px 14px', color: 'white' }}>
                      {formatMoney(inv.totalAmount, inv.currency)}
                      <div style={{ fontSize: '0.75rem', color: '#64748b' }}>{inv.lineCount} lines</div>
                    </td>
                    <td style={{ padding: '12px 14px' }}>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        <button type="button" className="btn btn-secondary" style={{ fontSize: '0.78rem', padding: '4px 10px' }} onClick={() => openDetail(inv.id)}>
                          View
                        </button>
                        {inv.status === 0 && (
                          <button type="button" className="btn btn-primary" style={{ fontSize: '0.78rem', padding: '4px 10px' }} disabled={actionBusy} onClick={() => runIssue(inv.id)}>
                            Issue
                          </button>
                        )}
                        {inv.status === 1 && (
                          <button type="button" className="btn btn-primary" style={{ fontSize: '0.78rem', padding: '4px 10px' }} disabled={actionBusy} onClick={() => { setPayModal(inv.id); setPayRef(''); setPayNotes(''); }}>
                            Mark paid
                          </button>
                        )}
                        {(inv.status === 0 || inv.status === 1) && (
                          <button type="button" className="btn btn-secondary" style={{ fontSize: '0.78rem', padding: '4px 10px' }} disabled={actionBusy} onClick={() => { setVoidModal(inv.id); setVoidReason(''); }}>
                            Void
                          </button>
                        )}
                        <button type="button" className="btn btn-secondary" style={{ fontSize: '0.78rem', padding: '4px 10px' }} onClick={() => runExport(inv.id, inv.invoiceNumber)}>
                          CSV
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {totalCount > 20 && (
        <div style={{ display: 'flex', gap: 8, marginTop: 12, justifyContent: 'flex-end' }}>
          <button type="button" className="btn btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</button>
          <span style={{ color: '#94a3b8', alignSelf: 'center' }}>Page {page}</span>
          <button type="button" className="btn btn-secondary" disabled={page * 20 >= totalCount} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      )}

      {(detail || detailLoading) && (
        <div style={{
          marginTop: '1.5rem',
          background: '#1e293b',
          borderRadius: 12,
          padding: '1.25rem',
          border: '1px solid rgba(255,255,255,0.06)',
        }}>
          {detailLoading ? (
            <div style={{ color: '#94a3b8' }}>Loading detail…</div>
          ) : detail && (
            <>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', marginBottom: '1rem' }}>
                <div>
                  <h2 style={{ margin: 0, color: 'white', fontSize: '1.15rem' }}>{detail.invoiceNumber}</h2>
                  <p style={{ margin: '4px 0 0', color: '#94a3b8', fontSize: '0.85rem' }}>
                    {detail.periodStart} → {detail.periodEnd} · {BILLING_TYPES[detail.billingTypeSnapshot]} · <StatusBadge status={detail.status} />
                  </p>
                </div>
                <div style={{ color: 'white', fontSize: '1.2rem', fontWeight: 700 }}>
                  {formatMoney(detail.totalAmount, detail.currency)}
                </div>
              </div>
              {Number(detail.totalAmount) === 0 && (
                <p style={{ color: '#fbbf24', fontSize: '0.85rem', marginBottom: '0.75rem' }}>
                  Zero total — no billable vendor leases / usage lines for this period.
                </p>
              )}
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                  <thead>
                    <tr style={{ color: '#94a3b8', textAlign: 'left', borderBottom: '1px solid #334155' }}>
                      <th style={{ padding: '8px 10px' }}>Type</th>
                      <th style={{ padding: '8px 10px' }}>Description</th>
                      <th style={{ padding: '8px 10px' }}>Qty</th>
                      <th style={{ padding: '8px 10px' }}>Unit</th>
                      <th style={{ padding: '8px 10px' }}>Amount</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(detail.lines || []).map((line) => (
                      <tr key={line.id} style={{ borderBottom: '1px solid #0f172a' }}>
                        <td style={{ padding: '8px 10px', color: '#cbd5e1' }}>{LINE_TYPES[line.lineType] ?? line.lineType}</td>
                        <td style={{ padding: '8px 10px', color: 'white' }}>{line.description}</td>
                        <td style={{ padding: '8px 10px', color: '#cbd5e1' }}>{line.quantity}</td>
                        <td style={{ padding: '8px 10px', color: '#cbd5e1' }}>{formatMoney(line.unitAmount, detail.currency)}</td>
                        <td style={{ padding: '8px 10px', color: 'white' }}>{formatMoney(line.amount, detail.currency)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {detail.paymentReference && (
                <p style={{ marginTop: 12, color: '#94a3b8', fontSize: '0.85rem' }}>
                  Payment ref: {detail.paymentReference}
                  {detail.paymentNotes ? ` · ${detail.paymentNotes}` : ''}
                </p>
              )}
              {detail.voidReason && (
                <p style={{ marginTop: 12, color: '#f87171', fontSize: '0.85rem' }}>
                  Voided: {detail.voidReason}
                </p>
              )}
            </>
          )}
        </div>
      )}

      {payModal && (
        <div style={{
          position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)', display: 'flex',
          alignItems: 'center', justifyContent: 'center', zIndex: 50, padding: 16,
        }}>
          <div style={{ background: '#1e293b', borderRadius: 12, padding: 24, maxWidth: 420, width: '100%', border: '1px solid #334155' }}>
            <h3 style={{ marginTop: 0, color: 'white' }}>Mark invoice paid</h3>
            <label style={{ display: 'block', color: '#94a3b8', fontSize: '0.85rem', marginBottom: 4 }}>Payment reference (optional)</label>
            <input
              value={payRef}
              onChange={(e) => setPayRef(e.target.value)}
              placeholder="Bank transfer / cheque ref"
              style={{ width: '100%', marginBottom: 12, padding: 10, background: '#0f172a', border: '1px solid #334155', borderRadius: 6, color: 'white' }}
            />
            <label style={{ display: 'block', color: '#94a3b8', fontSize: '0.85rem', marginBottom: 4 }}>Notes (optional)</label>
            <textarea
              value={payNotes}
              onChange={(e) => setPayNotes(e.target.value)}
              rows={3}
              style={{ width: '100%', marginBottom: 16, padding: 10, background: '#0f172a', border: '1px solid #334155', borderRadius: 6, color: 'white', resize: 'vertical' }}
            />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
              <button type="button" className="btn btn-secondary" onClick={() => setPayModal(null)}>Cancel</button>
              <button type="button" className="btn btn-primary" disabled={actionBusy} onClick={runMarkPaid}>Confirm paid</button>
            </div>
          </div>
        </div>
      )}

      {voidModal && (
        <div style={{
          position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)', display: 'flex',
          alignItems: 'center', justifyContent: 'center', zIndex: 50, padding: 16,
        }}>
          <div style={{ background: '#1e293b', borderRadius: 12, padding: 24, maxWidth: 420, width: '100%', border: '1px solid #334155' }}>
            <h3 style={{ marginTop: 0, color: 'white' }}>Void invoice</h3>
            <label style={{ display: 'block', color: '#94a3b8', fontSize: '0.85rem', marginBottom: 4 }}>Reason</label>
            <textarea
              value={voidReason}
              onChange={(e) => setVoidReason(e.target.value)}
              rows={3}
              placeholder="At least 3 characters"
              style={{ width: '100%', marginBottom: 16, padding: 10, background: '#0f172a', border: '1px solid #334155', borderRadius: 6, color: 'white', resize: 'vertical' }}
            />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
              <button type="button" className="btn btn-secondary" onClick={() => setVoidModal(null)}>Cancel</button>
              <button type="button" className="btn btn-primary" disabled={actionBusy} onClick={runVoid}>Void invoice</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default CompanyInvoices;
