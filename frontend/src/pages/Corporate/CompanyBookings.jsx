import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';

const BOOKING_STATUS = [
  'Pending',
  'Confirmed',
  'In Progress',
  'Completed',
  'Cancelled',
  'Expired',
  'Awaiting Payment',
  'Rejected',
  'Extension Pending',
  'Extension Payment Due',
];

const STATUS_COLORS = {
  0: '#f59e0b',
  1: '#10b981',
  2: '#6366f1',
  3: '#22c55e',
  4: '#ef4444',
  5: '#9ca3af',
  6: '#8b5cf6',
  7: '#ef4444',
  8: '#f59e0b',
  9: '#8b5cf6',
};

const SLOT_TYPES = {
  0: 'Fixed',
  1: 'Shared',
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
    {BOOKING_STATUS[status] ?? `Status ${status}`}
  </span>
);

const formatRange = (start, end) => {
  const s = new Date(start);
  const e = new Date(end);
  return `${s.toLocaleString()} → ${e.toLocaleString()}`;
};

const formatMoney = (value) => {
  const n = Number(value);
  if (Number.isNaN(n)) return '—';
  return n.toLocaleString(undefined, { style: 'currency', currency: 'INR', maximumFractionDigits: 2 });
};

const toDateInputValue = (d) => {
  if (!d) return '';
  return d.toISOString().slice(0, 10);
};

const startOfDayUtcIso = (dateStr) => {
  if (!dateStr) return null;
  return new Date(`${dateStr}T00:00:00.000Z`).toISOString();
};

const endOfDayUtcIso = (dateStr) => {
  if (!dateStr) return null;
  // exclusive upper bound: next day 00:00 UTC
  const d = new Date(`${dateStr}T00:00:00.000Z`);
  d.setUTCDate(d.getUTCDate() + 1);
  return d.toISOString();
};

const defaultFrom = () => {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - 30);
  return toDateInputValue(d);
};

const defaultTo = () => toDateInputValue(new Date());

const CompanyBookings = () => {
  const { activeCompanyId, companyDetails, isCorporateMode } = useCompany();
  const navigate = useNavigate();

  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [statusFilter, setStatusFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [fromDate, setFromDate] = useState(defaultFrom);
  const [toDate, setToDate] = useState(defaultTo);

  useEffect(() => {
    if (!isCorporateMode) {
      navigate('/dashboard', { replace: true });
    }
  }, [isCorporateMode, navigate]);

  const filterArgs = useCallback(() => {
    let isVisitor;
    if (typeFilter === 'visitor') isVisitor = true;
    else if (typeFilter === 'employee') isVisitor = false;
    return {
      status: statusFilter === 'all' ? undefined : Number(statusFilter),
      isVisitor,
      fromUtc: startOfDayUtcIso(fromDate),
      toUtc: endOfDayUtcIso(toDate),
    };
  }, [statusFilter, typeFilter, fromDate, toDate]);

  const loadBookings = useCallback(async () => {
    if (!isCorporateMode) return;
    setLoading(true);
    try {
      const response = await corporateService.getBookings(page, pageSize, filterArgs());
      if (response.success && response.data) {
        setBookings(response.data.bookings || []);
        setTotalCount(response.data.totalCount ?? 0);
      } else {
        toast.error(response.message || 'Failed to load corporate bookings');
        setBookings([]);
        setTotalCount(0);
      }
    } catch {
      toast.error('Could not reach server');
      setBookings([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [isCorporateMode, page, pageSize, filterArgs]);

  useEffect(() => {
    loadBookings();
  }, [loadBookings, activeCompanyId]);

  // Reset to page 1 when filters change
  useEffect(() => {
    setPage(1);
  }, [statusFilter, typeFilter, fromDate, toDate]);

  const handleExport = async () => {
    setExporting(true);
    try {
      const { blob, fileName } = await corporateService.exportBookings(filterArgs());
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName || `corporate-bookings-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
      toast.success('CSV export downloaded');
    } catch (err) {
      toast.error(err?.message || 'Export failed');
    } finally {
      setExporting(false);
    }
  };

  const canCancel = (status) => {
    // Confirmed, In Progress, Pending, Awaiting Payment, extension states
    return [0, 1, 2, 6, 8, 9].includes(Number(status));
  };

  const handleCancel = async (booking) => {
    if (!canCancel(booking.bookingStatus)) return;
    const ref = booking.bookingReference || booking.bookingId;
    if (!window.confirm(`Cancel corporate booking ${ref}? This frees the slot for waitlist auto-promotion.`)) {
      return;
    }
    try {
      const response = await corporateService.cancelBooking(
        booking.bookingId,
        'Cancelled from corporate bookings'
      );
      if (response.success) {
        toast.success(response.message || 'Booking cancelled');
        loadBookings();
      } else {
        toast.error(response.message || 'Failed to cancel booking');
      }
    } catch (err) {
      toast.error(err?.message || 'Failed to cancel booking');
    }
  };

  if (!isCorporateMode) return null;

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const totalAmountOnPage = bookings.reduce((sum, b) => sum + (Number(b.totalAmount) || 0), 0);

  return (
    <div className="container" style={{ padding: '2rem 0', color: '#f1f5f9' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', marginBottom: '1.5rem', flexWrap: 'wrap' }}>
        <div>
          <h1 style={{ margin: '0 0 0.35rem 0', color: 'white', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <span style={{ fontSize: '2rem' }}>📅</span> Corporate Bookings
          </h1>
          <p style={{ color: '#94a3b8', margin: 0 }}>
            {companyDetails?.name ? `${companyDetails.name} · ` : ''}
            Admins see company-wide reservations; employees see their own. Export uses the active filters.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <button type="button" className="btn btn-secondary" onClick={loadBookings} disabled={loading}>
            Refresh
          </button>
          <button type="button" className="btn btn-primary" onClick={handleExport} disabled={exporting || loading}>
            {exporting ? 'Exporting…' : 'Export CSV'}
          </button>
        </div>
      </div>

      <div style={{
        display: 'flex',
        gap: '0.75rem',
        flexWrap: 'wrap',
        marginBottom: '1.25rem',
        background: '#1e293b',
        borderRadius: '12px',
        padding: '1rem',
        border: '1px solid rgba(255,255,255,0.05)',
      }}>
        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem', fontSize: '0.8rem', color: '#94a3b8' }}>
          Status
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{
              background: '#0f172a',
              color: '#e2e8f0',
              border: '1px solid #334155',
              borderRadius: '8px',
              padding: '0.45rem 0.65rem',
              minWidth: '160px',
            }}
          >
            <option value="all">All statuses</option>
            {BOOKING_STATUS.map((label, idx) => (
              <option key={label} value={String(idx)}>{label}</option>
            ))}
          </select>
        </label>
        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem', fontSize: '0.8rem', color: '#94a3b8' }}>
          Type
          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            style={{
              background: '#0f172a',
              color: '#e2e8f0',
              border: '1px solid #334155',
              borderRadius: '8px',
              padding: '0.45rem 0.65rem',
              minWidth: '140px',
            }}
          >
            <option value="all">Employee + Visitor</option>
            <option value="employee">Employee only</option>
            <option value="visitor">Visitor only</option>
          </select>
        </label>
        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem', fontSize: '0.8rem', color: '#94a3b8' }}>
          From (UTC)
          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
            style={{
              background: '#0f172a',
              color: '#e2e8f0',
              border: '1px solid #334155',
              borderRadius: '8px',
              padding: '0.45rem 0.65rem',
            }}
          />
        </label>
        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem', fontSize: '0.8rem', color: '#94a3b8' }}>
          To (UTC)
          <input
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
            style={{
              background: '#0f172a',
              color: '#e2e8f0',
              border: '1px solid #334155',
              borderRadius: '8px',
              padding: '0.45rem 0.65rem',
            }}
          />
        </label>
        <div style={{ marginLeft: 'auto', alignSelf: 'flex-end', color: '#64748b', fontSize: '0.85rem', textAlign: 'right' }}>
          <div>{totalCount} booking{totalCount === 1 ? '' : 's'} match</div>
          <div style={{ color: '#cbd5e1' }}>Page amount: {formatMoney(totalAmountOnPage)}</div>
        </div>
      </div>

      {loading ? (
        <div style={{ padding: '3rem', textAlign: 'center' }}><div className="spinner" /></div>
      ) : bookings.length === 0 ? (
        <div style={{
          background: '#1e293b',
          borderRadius: '12px',
          padding: '2.5rem',
          textAlign: 'center',
          border: '1px solid rgba(255,255,255,0.05)',
          color: '#94a3b8',
        }}>
          No corporate bookings match the current filters.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
          {bookings.map((booking) => (
            <div
              key={booking.id}
              style={{
                background: '#1e293b',
                borderRadius: '12px',
                padding: '1.15rem 1.35rem',
                border: '1px solid rgba(255,255,255,0.05)',
                display: 'grid',
                gridTemplateColumns: 'minmax(180px, 1.4fr) minmax(160px, 1fr) minmax(200px, 1.2fr) auto',
                gap: '1rem',
                alignItems: 'center',
              }}
            >
              <div>
                <div style={{ color: 'white', fontWeight: 600, marginBottom: '0.25rem' }}>
                  {booking.parkingSpaceTitle || 'Corporate parking'}
                </div>
                <div style={{ color: '#94a3b8', fontSize: '0.82rem' }}>
                  {booking.bookingReference || booking.bookingId?.slice?.(0, 8) || booking.id}
                  {booking.slotNumber != null && ` · Slot #${booking.slotNumber}`}
                  {booking.slotType != null && ` · ${SLOT_TYPES[booking.slotType] || 'Slot'}`}
                </div>
              </div>

              <div>
                {booking.isVisitorBooking ? (
                  <>
                    <div style={{ color: '#e2e8f0', fontWeight: 600 }}>
                      👤 {booking.visitorName || 'Visitor'}
                    </div>
                    <div style={{ color: '#94a3b8', fontSize: '0.82rem' }}>
                      {booking.visitorLicensePlate || booking.vehicleNumber || 'No plate'} · Visitor
                    </div>
                  </>
                ) : (
                  <>
                    <div style={{ color: '#e2e8f0', fontWeight: 600 }}>
                      {booking.memberName || 'Employee'}
                    </div>
                    <div style={{ color: '#94a3b8', fontSize: '0.82rem' }}>
                      {booking.memberEmail || booking.vehicleNumber || 'Employee booking'}
                    </div>
                  </>
                )}
              </div>

              <div style={{ color: '#cbd5e1', fontSize: '0.85rem' }}>
                <div>{formatRange(booking.startDateTime, booking.endDateTime)}</div>
                <div style={{ color: '#94a3b8', marginTop: '0.25rem' }}>{formatMoney(booking.totalAmount)}</div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '0.4rem' }}>
                <StatusBadge status={booking.bookingStatus} />
                {canCancel(booking.bookingStatus) && (
                  <button
                    type="button"
                    className="btn btn-secondary"
                    style={{ padding: '0.35rem 0.75rem', fontSize: '0.8rem' }}
                    onClick={() => handleCancel(booking)}
                  >
                    Cancel
                  </button>
                )}
                {booking.qrCodeToken && (
                  <span style={{ color: '#64748b', fontSize: '0.75rem' }} title={booking.qrCodeToken}>
                    QR ready
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '1rem', marginTop: '1.5rem' }}>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={page <= 1 || loading}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Previous
          </button>
          <span style={{ color: '#94a3b8', fontSize: '0.9rem' }}>
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={page >= totalPages || loading}
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
};

export default CompanyBookings;
