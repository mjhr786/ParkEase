import { useState, useEffect, useCallback, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';
import corporateService from '../services/corporateService';
import { handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';

const BOOKING_STATUS = ['Pending', 'Confirmed', 'InProgress', 'Completed', 'Cancelled', 'Expired', 'Awaiting Payment', 'Rejected', 'Extension Pending', 'Extension Payment Due'];
const STATUS_COLORS = {
    0: 'var(--color-warning)',
    1: 'var(--color-success)',
    2: 'var(--color-primary)',
    3: 'var(--color-success)',
    4: 'var(--color-error)',
    5: 'var(--color-text-muted)',
    6: 'var(--color-secondary)',
    7: 'var(--color-error)',
    8: 'var(--color-warning)',
    9: 'var(--color-secondary)',
};

const ALLOC_STATUS = {
    0: { label: 'Pending Approval', color: 'var(--color-warning)' },
    1: { label: 'Active', color: 'var(--color-success)' },
    2: { label: 'Rejected', color: 'var(--color-error)' },
    3: { label: 'Expired', color: 'var(--color-text-secondary)' },
};

const REFRESH_TRIGGERS = ['booking.requested', 'payment.completed', 'booking.cancelled', 'booking.checkin', 'booking.checkout', 'extension.requested', 'extension.approved', 'extension.rejected'];

const formatMoney = (value) => {
    const n = Number(value);
    if (Number.isNaN(n)) return '—';
    return n.toLocaleString(undefined, { style: 'currency', currency: 'INR', maximumFractionDigits: 0 });
};

/** PricingType: 0=Hourly; Daily/Weekly/Monthly show dates only. */
const formatBookingRangeValue = (dateTime, pricingType) => {
    const d = new Date(dateTime);
    if (Number.isNaN(d.getTime())) return '—';
    if (Number(pricingType) > 0) {
        return d.toLocaleDateString(undefined, {
            year: 'numeric',
            month: 'numeric',
            day: 'numeric',
        });
    }
    return d.toLocaleString();
};

export default function VendorBookings() {
    const [bookings, setBookings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState('requests');
    const [allocStatusFilter, setAllocStatusFilter] = useState('all');
    const [allocations, setAllocations] = useState([]);
    const [processingId, setProcessingId] = useState(null);
    const [rejectReason, setRejectReason] = useState('');
    const [showRejectModal, setShowRejectModal] = useState(null);
    const [rejectingAllocation, setRejectingAllocation] = useState(false);

    const { subscribeToRefresh, triggerRefresh } = useNotificationContext();

    const showBookings = filter !== 'allocations';
    const showAllocations = filter === 'allocations' || filter === '';

    /** Keep header Vendor Inbox badge in sync after local approve/reject (SignalR notifies the other party only). */
    const refreshPendingBadge = useCallback((eventType) => {
        triggerRefresh?.(eventType);
    }, [triggerRefresh]);

    const extractBookings = (response) => {
        if (!response?.success || !response.data) return [];
        return Array.isArray(response.data)
            ? response.data
            : (response.data.bookings || response.data.items || []);
    };

    const fetchData = useCallback(async () => {
        setLoading(true);
        try {
            if (showBookings) {
                if (filter === 'requests') {
                    // Pending (0) + extension pending (8) — server-side so list matches badge count
                    const [pendingRes, extensionRes] = await Promise.all([
                        api.getVendorBookings({ status: 0, pageSize: 50 }),
                        api.getVendorBookings({ status: 8, pageSize: 50 }),
                    ]);
                    const merged = [...extractBookings(pendingRes), ...extractBookings(extensionRes)];
                    const byId = new Map(merged.map((b) => [b.id, b]));
                    setBookings([...byId.values()].filter((b) => b.status === 0 || b.status === 8));
                } else {
                    const params = (filter && filter !== '')
                        ? { status: filter, pageSize: 50 }
                        : { pageSize: 50 };
                    const response = await api.getVendorBookings(params);
                    setBookings(extractBookings(response));
                }
            } else {
                setBookings([]);
            }

            if (showAllocations) {
                try {
                    const allocRes = await corporateService.getVendorAllocations();
                    if (allocRes.success && allocRes.data) {
                        setAllocations(allocRes.data);
                    } else {
                        setAllocations([]);
                    }
                } catch (allocErr) {
                    console.error('Failed to load corporate vendor allocations', allocErr);
                    setAllocations([]);
                }
            } else {
                setAllocations([]);
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to load vendor inbox'));
            setBookings([]);
        }
        setLoading(false);
    }, [filter, showBookings, showAllocations]);

    useEffect(() => {
        fetchData();
    }, [fetchData]);

    useEffect(() => {
        const unsubscribe = subscribeToRefresh('VendorBookings', REFRESH_TRIGGERS, () => {
            fetchData();
        });
        return unsubscribe;
    }, [subscribeToRefresh, fetchData]);

    const filteredAllocations = useMemo(() => {
        if (allocStatusFilter === 'all') return allocations;
        const status = Number(allocStatusFilter);
        return allocations.filter((a) => a.status === status);
    }, [allocations, allocStatusFilter]);

    const pendingAllocCount = useMemo(
        () => allocations.filter((a) => a.status === 0).length,
        [allocations]
    );

    const handleApprove = async (id) => {
        setProcessingId(id);
        try {
            const response = await api.approveBooking(id);
            if (response.success) {
                showToast.success('Booking approved successfully!');
                refreshPendingBadge('booking.approved');
                fetchData();
            } else {
                showToast.error(response.message || 'Failed to approve booking');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to approve booking'));
        }
        setProcessingId(null);
    };

    const handleReject = async (id) => {
        setProcessingId(id);
        try {
            const response = await api.rejectBooking(id, rejectReason);
            if (response.success) {
                showToast.success('Booking rejected');
                setShowRejectModal(null);
                setRejectReason('');
                refreshPendingBadge('booking.rejected');
                fetchData();
            } else {
                showToast.error(response.message || 'Failed to reject booking');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to reject booking'));
        }
        setProcessingId(null);
    };

    const handleApproveExtension = async (id) => {
        setProcessingId(id);
        try {
            const response = await api.approveExtension(id);
            if (response.success) {
                showToast.success('Extension approved successfully!');
                refreshPendingBadge('extension.approved');
                fetchData();
            } else {
                showToast.error(response.message || 'Failed to approve extension');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to approve extension'));
        }
        setProcessingId(null);
    };

    const handleRejectExtension = async (id) => {
        const reason = window.prompt('Reason for rejecting the extension (optional):');
        if (reason === null) return;
        setProcessingId(id);
        try {
            const response = await api.rejectExtension(id, reason || 'Rejected by owner');
            if (response.success) {
                showToast.success('Extension rejected');
                refreshPendingBadge('extension.rejected');
                fetchData();
            } else {
                showToast.error(response.message || 'Failed to reject extension');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to reject extension'));
        }
        setProcessingId(null);
    };

    const handleApproveAllocation = async (id) => {
        setProcessingId(id);
        try {
            const response = await corporateService.approveAllocation(id);
            if (response.success) {
                showToast.success('Corporate allocation approved successfully!');
                fetchData();
            } else {
                showToast.error(response.message || 'Failed to approve allocation');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to approve allocation'));
        }
        setProcessingId(null);
    };

    const handleRejectAllocation = async (id) => {
        setProcessingId(id);
        try {
            const response = await corporateService.rejectAllocation(id, rejectReason);
            if (response.success) {
                showToast.success('Corporate allocation rejected');
                setShowRejectModal(null);
                setRejectReason('');
                setRejectingAllocation(false);
                fetchData();
            } else {
                showToast.error(response.message || 'Failed to reject allocation');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to reject allocation'));
        }
        setProcessingId(null);
    };

    const hasBookings = bookings.length > 0;
    const hasAllocations = filteredAllocations.length > 0;
    const isEmpty = !loading
        && (!showBookings || !hasBookings)
        && (!showAllocations || !hasAllocations);

    return (
        <div className="page">
            <div className="container">
                <div className="flex-between mb-3" style={{ flexWrap: 'wrap', gap: '1rem' }}>
                    <div>
                        <h1 style={{ margin: '0 0 0.25rem 0' }}>
                            Vendor Inbox{' '}
                            <Link to="/my/access-scan" className="btn btn-outline" style={{ fontSize: '0.8rem', marginLeft: '0.5rem', verticalAlign: 'middle' }}>
                                📱 Scan pass
                            </Link>
                        </h1>
                        <p style={{ margin: 0, color: 'var(--color-text-muted)', fontSize: '0.9rem' }}>
                            Marketplace booking requests and corporate bulk allocation approvals.
                            {pendingAllocCount > 0 && (
                                <span style={{ marginLeft: '0.5rem', color: 'var(--color-warning)', fontWeight: 600 }}>
                                    {pendingAllocCount} allocation{pendingAllocCount === 1 ? '' : 's'} pending
                                </span>
                            )}
                        </p>
                    </div>
                    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'center' }}>
                        <select
                            className="form-select"
                            style={{ width: 'auto' }}
                            value={filter}
                            onChange={(e) => setFilter(e.target.value)}
                        >
                            <option value="requests">Pending booking requests</option>
                            <option value="allocations">Corporate allocations</option>
                            <option value="">All bookings + allocations</option>
                            {BOOKING_STATUS.map((status, i) => (
                                <option key={i} value={i}>{status}</option>
                            ))}
                        </select>
                        {showAllocations && (
                            <select
                                className="form-select"
                                style={{ width: 'auto' }}
                                value={allocStatusFilter}
                                onChange={(e) => setAllocStatusFilter(e.target.value)}
                                title="Filter corporate allocations by status"
                            >
                                <option value="all">All allocation statuses</option>
                                <option value="0">Pending allocations</option>
                                <option value="1">Active allocations</option>
                                <option value="2">Rejected allocations</option>
                                <option value="3">Expired allocations</option>
                            </select>
                        )}
                        <button type="button" className="btn btn-secondary" onClick={fetchData} disabled={loading}>
                            Refresh
                        </button>
                    </div>
                </div>

                {loading ? (
                    <div className="loading">
                        <div className="spinner" />
                    </div>
                ) : isEmpty ? (
                    <div className="empty-state">
                        <div className="empty-icon">📋</div>
                        <h3>Nothing to review</h3>
                        <p>
                            {filter === 'allocations'
                                ? 'No corporate allocations match this filter.'
                                : filter === 'requests'
                                    ? 'No pending booking or extension requests.'
                                    : 'No items found for this filter.'}
                        </p>
                    </div>
                ) : (
                    <div className="grid" style={{ gap: '1rem' }}>
                        {showBookings && hasBookings && (
                            <>
                                {filter === '' && (
                                    <h2 style={{ margin: '0.5rem 0 0', fontSize: '1rem', color: 'var(--color-text-muted)' }}>
                                        Marketplace bookings ({bookings.length})
                                    </h2>
                                )}
                                {bookings.map((booking) => (
                                    <div key={booking.id} className="card hover-card">
                                        <div className="flex-between">
                                            <div>
                                                <h3 className="card-title">{booking.parkingSpaceTitle}</h3>
                                                <div className="card-subtitle">
                                                    Requested by: <strong>{booking.userName}</strong>
                                                </div>
                                            </div>
                                            <div style={{ textAlign: 'right' }}>
                                                <span
                                                    className="parking-tag"
                                                    style={{
                                                        background: `${STATUS_COLORS[booking.status]}20`,
                                                        color: STATUS_COLORS[booking.status],
                                                        border: `1px solid ${STATUS_COLORS[booking.status]}50`,
                                                    }}
                                                >
                                                    {BOOKING_STATUS[booking.status]}
                                                </span>
                                                <div className="parking-price mt-1">₹{booking.totalAmount}</div>
                                            </div>
                                        </div>

                                        <div className="grid grid-4 mt-2" style={{ fontSize: '0.9rem' }}>
                                            <div>
                                                <small style={{ color: 'var(--color-text-muted)' }}>Reference</small>
                                                <div>{booking.bookingReference}</div>
                                            </div>
                                            <div>
                                                <small style={{ color: 'var(--color-text-muted)' }}>Start</small>
                                                <div>{formatBookingRangeValue(booking.startDateTime, booking.pricingType)}</div>
                                            </div>
                                            <div>
                                                <small style={{ color: 'var(--color-text-muted)' }}>End</small>
                                                <div>{formatBookingRangeValue(booking.endDateTime, booking.pricingType)}</div>
                                            </div>
                                            <div>
                                                <small style={{ color: 'var(--color-text-muted)' }}>Vehicle</small>
                                                <div>
                                                    {booking.vehicleNumber || 'Not specified'}
                                                    {booking.vehicleColor ? ` (${booking.vehicleColor})` : ''}
                                                </div>
                                            </div>
                                            {booking.slotNumber != null && (
                                                <div>
                                                    <small style={{ color: 'var(--color-text-muted)' }}>Slot</small>
                                                    <div>
                                                        <span style={{
                                                            display: 'inline-flex',
                                                            alignItems: 'center',
                                                            gap: '0.3rem',
                                                            background: 'rgba(99,102,241,0.15)',
                                                            color: 'var(--color-accent-light)',
                                                            border: '1px solid rgba(99,102,241,0.35)',
                                                            borderRadius: '6px',
                                                            padding: '0.1rem 0.5rem',
                                                            fontWeight: 600,
                                                            fontSize: '0.85rem',
                                                        }}
                                                        >
                                                            🅿️ P{booking.slotNumber}
                                                        </span>
                                                    </div>
                                                </div>
                                            )}
                                            {(booking.bayLabel || booking.facilityLevel || booking.facilityZone) && (
                                                <div>
                                                    <small style={{ color: 'var(--color-text-muted)' }}>Bay</small>
                                                    <div style={{ fontWeight: 600 }}>
                                                        {[booking.facilityLevel, booking.facilityZone, booking.bayLabel].filter(Boolean).join(' · ')}
                                                    </div>
                                                </div>
                                            )}
                                        </div>

                                        {(booking.isBayGuidanceEnabled || booking.valetStatus > 0) && [1, 2, 6, 8, 9].includes(booking.status) && (
                                            <div style={{
                                                marginTop: '0.6rem',
                                                padding: '0.55rem 0.7rem',
                                                background: 'rgba(59,130,246,0.08)',
                                                borderRadius: 'var(--radius-sm)',
                                                fontSize: '0.85rem',
                                            }}>
                                                {booking.isBayGuidanceEnabled && (
                                                    <div className="flex gap-1" style={{ flexWrap: 'wrap', marginBottom: booking.valetStatus > 0 ? '0.5rem' : 0 }}>
                                                        <button
                                                            className="btn btn-outline"
                                                            type="button"
                                                            style={{ fontSize: '0.8rem' }}
                                                            disabled={processingId === booking.id}
                                                            onClick={async () => {
                                                                const level = window.prompt('Facility level', booking.facilityLevel || 'P1');
                                                                if (level === null) return;
                                                                const zone = window.prompt('Zone', booking.facilityZone || '');
                                                                if (zone === null) return;
                                                                const bay = window.prompt('Bay label', booking.bayLabel || (booking.slotNumber ? `B-${booking.slotNumber}` : ''));
                                                                if (bay === null) return;
                                                                setProcessingId(booking.id);
                                                                try {
                                                                    const res = await api.assignBay(booking.id, {
                                                                        facilityLevel: level || null,
                                                                        facilityZone: zone || null,
                                                                        bayLabel: bay || null,
                                                                    });
                                                                    if (res.success) {
                                                                        showToast.success('Bay assignment updated');
                                                                        fetchData();
                                                                    } else {
                                                                        showToast.error(res.message || 'Failed to assign bay');
                                                                    }
                                                                } catch (err) {
                                                                    showToast.error(handleApiError(err, 'Failed to assign bay'));
                                                                }
                                                                setProcessingId(null);
                                                            }}
                                                        >
                                                            Assign bay
                                                        </button>
                                                    </div>
                                                )}
                                                {booking.valetStatus > 0 && booking.valetStatus < 4 && (
                                                    <div className="flex gap-1" style={{ flexWrap: 'wrap', alignItems: 'center' }}>
                                                        <span style={{ color: 'var(--color-secondary)', fontWeight: 600 }}>
                                                            Valet: {['None', 'Requested', 'Retrieving', 'Ready', 'Completed', 'Cancelled'][booking.valetStatus] || booking.valetStatus}
                                                            {booking.valetTargetReadyAt && (
                                                                <span style={{ fontWeight: 400, color: 'var(--color-text-muted)' }}>
                                                                    {' '}· target {new Date(booking.valetTargetReadyAt).toLocaleTimeString()}
                                                                </span>
                                                            )}
                                                        </span>
                                                        {booking.valetStatus === 1 && (
                                                            <button
                                                                className="btn btn-primary"
                                                                type="button"
                                                                style={{ fontSize: '0.8rem' }}
                                                                disabled={processingId === booking.id}
                                                                onClick={async () => {
                                                                    setProcessingId(booking.id);
                                                                    try {
                                                                        const res = await api.acknowledgeValet(booking.id);
                                                                        if (res.success) {
                                                                            showToast.success('Valet acknowledged');
                                                                            fetchData();
                                                                        } else showToast.error(res.message || 'Failed');
                                                                    } catch (err) {
                                                                        showToast.error(handleApiError(err, 'Failed'));
                                                                    }
                                                                    setProcessingId(null);
                                                                }}
                                                            >
                                                                Acknowledge
                                                            </button>
                                                        )}
                                                        {[1, 2].includes(booking.valetStatus) && (
                                                            <button
                                                                className="btn btn-primary"
                                                                type="button"
                                                                style={{ fontSize: '0.8rem' }}
                                                                disabled={processingId === booking.id}
                                                                onClick={async () => {
                                                                    setProcessingId(booking.id);
                                                                    try {
                                                                        const res = await api.markValetReady(booking.id);
                                                                        if (res.success) {
                                                                            showToast.success('Vehicle ready — guest notified');
                                                                            fetchData();
                                                                        } else showToast.error(res.message || 'Failed');
                                                                    } catch (err) {
                                                                        showToast.error(handleApiError(err, 'Failed'));
                                                                    }
                                                                    setProcessingId(null);
                                                                }}
                                                            >
                                                                Mark ready
                                                            </button>
                                                        )}
                                                        {[1, 2, 3].includes(booking.valetStatus) && (
                                                            <button
                                                                className="btn btn-outline"
                                                                type="button"
                                                                style={{ fontSize: '0.8rem' }}
                                                                disabled={processingId === booking.id}
                                                                onClick={async () => {
                                                                    setProcessingId(booking.id);
                                                                    try {
                                                                        const res = await api.completeValet(booking.id);
                                                                        if (res.success) {
                                                                            showToast.success('Valet completed');
                                                                            fetchData();
                                                                        } else showToast.error(res.message || 'Failed');
                                                                    } catch (err) {
                                                                        showToast.error(handleApiError(err, 'Failed'));
                                                                    }
                                                                    setProcessingId(null);
                                                                }}
                                                            >
                                                                Complete
                                                            </button>
                                                        )}
                                                    </div>
                                                )}
                                            </div>
                                        )}

                                        {booking.status === 0 && (
                                            <div className="flex gap-1 mt-2">
                                                <button
                                                    className="btn btn-primary"
                                                    type="button"
                                                    onClick={() => handleApprove(booking.id)}
                                                    disabled={processingId === booking.id}
                                                >
                                                    {processingId === booking.id ? 'Processing...' : '✓ Approve'}
                                                </button>
                                                <button
                                                    className="btn btn-danger"
                                                    type="button"
                                                    onClick={() => {
                                                        setRejectingAllocation(false);
                                                        setShowRejectModal(booking.id);
                                                    }}
                                                    disabled={processingId === booking.id}
                                                >
                                                    ✗ Reject
                                                </button>
                                            </div>
                                        )}

                                        {booking.status === 8 && (
                                            <div className="flex gap-1 mt-2">
                                                <button
                                                    className="btn btn-primary"
                                                    type="button"
                                                    onClick={() => handleApproveExtension(booking.id)}
                                                    disabled={processingId === booking.id}
                                                >
                                                    {processingId === booking.id ? 'Processing...' : 'Approve Extension'}
                                                </button>
                                                <button
                                                    className="btn btn-danger"
                                                    type="button"
                                                    onClick={() => handleRejectExtension(booking.id)}
                                                    disabled={processingId === booking.id}
                                                >
                                                    Reject Extension
                                                </button>
                                            </div>
                                        )}

                                        {booking.status !== 0 && booking.status !== 8 && (
                                            <Link to={`/parking/${booking.parkingSpaceId}`} className="btn btn-secondary mt-2">
                                                View Parking
                                            </Link>
                                        )}
                                    </div>
                                ))}
                            </>
                        )}

                        {showAllocations && (
                            <>
                                {filter === '' && (
                                    <h2 style={{ margin: '0.75rem 0 0', fontSize: '1rem', color: 'var(--color-text-muted)' }}>
                                        Corporate allocations ({filteredAllocations.length})
                                    </h2>
                                )}
                                {filteredAllocations.map((allocation) => {
                                    const st = ALLOC_STATUS[allocation.status] || ALLOC_STATUS[3];
                                    return (
                                        <div
                                            key={`alloc-${allocation.id}`}
                                            className="card hover-card"
                                            style={{ borderLeft: '4px solid #8b5cf6' }}
                                        >
                                            <div className="flex-between">
                                                <div>
                                                    <h3 className="card-title">🏢 {allocation.parkingSpaceTitle}</h3>
                                                    <div className="card-subtitle">
                                                        Company: <strong>{allocation.companyName || 'Corporate client'}</strong>
                                                    </div>
                                                </div>
                                                <div style={{ textAlign: 'right' }}>
                                                    <span
                                                        className="parking-tag"
                                                        style={{
                                                            background: `${st.color}20`,
                                                            color: st.color,
                                                            border: `1px solid ${st.color}50`,
                                                        }}
                                                    >
                                                        {st.label}
                                                    </span>
                                                    <div className="parking-price mt-1">{formatMoney(allocation.monthlyRate)}/mo</div>
                                                </div>
                                            </div>

                                            <div className="grid grid-4 mt-2" style={{ fontSize: '0.9rem' }}>
                                                <div>
                                                    <small style={{ color: 'var(--color-text-muted)' }}>Total slots</small>
                                                    <div style={{ fontWeight: 'bold' }}>{allocation.totalSlots}</div>
                                                    {(allocation.fourWheeler || allocation.twoWheeler) && (
                                                        <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', marginTop: 4 }}>
                                                            4W {allocation.fourWheeler?.totalSlots ?? 0} · 2W {allocation.twoWheeler?.totalSlots ?? 0}
                                                        </div>
                                                    )}
                                                </div>
                                                <div>
                                                    <small style={{ color: 'var(--color-text-muted)' }}>Fixed / Shared</small>
                                                    <div>{allocation.fixedSlots ?? 0} / {allocation.sharedSlots ?? 0}</div>
                                                </div>
                                                <div>
                                                    <small style={{ color: 'var(--color-text-muted)' }}>Contract</small>
                                                    <div>
                                                        {new Date(allocation.startDate).toLocaleDateString()}
                                                        {' – '}
                                                        {new Date(allocation.endDate).toLocaleDateString()}
                                                    </div>
                                                </div>
                                                <div>
                                                    <small style={{ color: 'var(--color-text-muted)' }}>Lease ref</small>
                                                    <div>{allocation.leaseReference || '—'}</div>
                                                </div>
                                            </div>

                                            {allocation.status === 0 && (
                                                <div className="flex gap-1 mt-2 pt-2" style={{ borderTop: '1px solid var(--color-border)' }}>
                                                    <button
                                                        className="btn btn-primary"
                                                        type="button"
                                                        onClick={() => handleApproveAllocation(allocation.id)}
                                                        disabled={processingId === allocation.id}
                                                    >
                                                        {processingId === allocation.id ? 'Processing...' : '✓ Approve Allocation'}
                                                    </button>
                                                    <button
                                                        className="btn btn-danger"
                                                        type="button"
                                                        onClick={() => {
                                                            setShowRejectModal(allocation.id);
                                                            setRejectingAllocation(true);
                                                        }}
                                                        disabled={processingId === allocation.id}
                                                    >
                                                        ✗ Reject
                                                    </button>
                                                </div>
                                            )}

                                            {allocation.status === 1 && (
                                                <Link
                                                    to={`/parking/${allocation.parkingSpaceId}`}
                                                    className="btn btn-secondary mt-2"
                                                >
                                                    View parking space
                                                </Link>
                                            )}
                                        </div>
                                    );
                                })}
                            </>
                        )}
                    </div>
                )}

                {showRejectModal && (
                    <div style={{
                        position: 'fixed',
                        top: 0, left: 0, right: 0, bottom: 0,
                        background: 'rgba(0,0,0,0.7)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 1000,
                    }}
                    >
                        <div className="card" style={{ maxWidth: '400px', width: '100%' }}>
                            <h3 className="card-title">
                                {rejectingAllocation ? 'Reject allocation' : 'Reject booking'}
                            </h3>
                            <p className="card-subtitle mb-2">Optional reason for rejection:</p>
                            <textarea
                                className="form-input"
                                rows="3"
                                placeholder="Reason for rejection..."
                                value={rejectReason}
                                onChange={(e) => setRejectReason(e.target.value)}
                            />
                            <div className="flex gap-1 mt-2">
                                <button
                                    className="btn btn-danger"
                                    type="button"
                                    onClick={() => (rejectingAllocation
                                        ? handleRejectAllocation(showRejectModal)
                                        : handleReject(showRejectModal))}
                                    disabled={processingId === showRejectModal}
                                >
                                    {processingId === showRejectModal ? 'Rejecting...' : 'Confirm Reject'}
                                </button>
                                <button
                                    className="btn btn-secondary"
                                    type="button"
                                    onClick={() => {
                                        setShowRejectModal(null);
                                        setRejectReason('');
                                        setRejectingAllocation(false);
                                    }}
                                >
                                    Cancel
                                </button>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}

