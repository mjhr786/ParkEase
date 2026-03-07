import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';
import { handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';

const BOOKING_STATUS = ['Pending', 'Confirmed', 'InProgress', 'Completed', 'Cancelled', 'Expired', 'Awaiting Payment', 'Rejected', 'Extension Pending', 'Extension Payment Due'];
const STATUS_COLORS = {
    0: '#f59e0b', // Pending
    1: '#10b981', // Confirmed
    2: '#6366f1', // InProgress
    3: '#22c55e', // Completed
    4: '#ef4444', // Cancelled
    5: '#9ca3af', // Expired
    6: '#8b5cf6', // Awaiting Payment (purple)
    7: '#ef4444', // Rejected
    8: '#f59e0b', // Extension Pending
    9: '#8b5cf6', // Extension Payment Due
};

// Notification types that should trigger a refresh of vendor bookings
const REFRESH_TRIGGERS = [
    'booking.requested',   // New booking request
    'payment.completed',   // User paid
    'booking.cancelled',   // User cancelled
    'booking.checkin',     // User checked in
    'booking.checkout',    // User checked out
    'extension.requested',
    'extension.approved',
    'extension.rejected'
];

export default function VendorBookings() {
    const [bookings, setBookings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState('requests'); // Pending + extension pending
    const [processingId, setProcessingId] = useState(null);
    const [rejectReason, setRejectReason] = useState('');
    const [showRejectModal, setShowRejectModal] = useState(null);

    const { subscribeToRefresh } = useNotificationContext();

    const fetchBookings = useCallback(async () => {
        setLoading(true);
        try {
            const params = (filter && filter !== 'requests') ? { status: filter } : {};
            const response = await api.getVendorBookings(params);
            if (response.success && response.data) {
                const bookingsData = Array.isArray(response.data)
                    ? response.data
                    : (response.data.bookings || response.data.items || []);
                if (filter === 'requests') {
                    setBookings(bookingsData.filter(b => b.status === 0 || b.status === 8));
                } else {
                    setBookings(bookingsData);
                }
            } else {
                setBookings([]);
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to load bookings'));
            setBookings([]);
        }
        setLoading(false);
    }, [filter]);

    // Load bookings when filter changes
    useEffect(() => {
        fetchBookings();
    }, [fetchBookings]);

    // Subscribe to real-time refresh events
    useEffect(() => {
        const unsubscribe = subscribeToRefresh('VendorBookings', REFRESH_TRIGGERS, () => {
            fetchBookings();
        });
        return unsubscribe;
    }, [subscribeToRefresh, fetchBookings]);

    const handleApprove = async (id) => {
        setProcessingId(id);
        try {
            const response = await api.approveBooking(id);
            if (response.success) {
                showToast.success('Booking approved successfully!');
                fetchBookings();
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
                fetchBookings();
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
                fetchBookings();
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
                fetchBookings();
            } else {
                showToast.error(response.message || 'Failed to reject extension');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to reject extension'));
        }
        setProcessingId(null);
    };

    return (
        <div className="page">
            <div className="container">
                <div className="flex-between mb-3">
                    <h1>Booking Requests</h1>
                    <select
                        className="form-select"
                        style={{ width: 'auto' }}
                        value={filter}
                        onChange={(e) => setFilter(e.target.value)}
                    >
                        <option value="requests">Pending Requests</option>
                        <option value="">All Bookings</option>
                        {BOOKING_STATUS.map((status, i) => (
                            <option key={i} value={i}>{status}</option>
                        ))}
                    </select>
                </div>

                {loading ? (
                    <div className="loading">
                        <div className="spinner"></div>
                    </div>
                ) : bookings.length === 0 ? (
                    <div className="empty-state">
                        <div className="empty-icon">📋</div>
                        <h3>No booking requests</h3>
                        <p>{filter === 'requests' ? 'No pending requests to review' : 'No bookings found with this status'}</p>
                    </div>
                ) : (
                    <div className="grid" style={{ gap: '1rem' }}>
                        {bookings.map(booking => (
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
                                        <div>{new Date(booking.startDateTime).toLocaleString()}</div>
                                    </div>
                                    <div>
                                        <small style={{ color: 'var(--color-text-muted)' }}>End</small>
                                        <div>{new Date(booking.endDateTime).toLocaleString()}</div>
                                    </div>
                                    <div>
                                        <small style={{ color: 'var(--color-text-muted)' }}>Vehicle</small>
                                        <div>{booking.vehicleNumber || 'Not specified'} {booking.vehicleColor ? `(${booking.vehicleColor})` : ''}</div>
                                    </div>
                                    {booking.slotNumber && (
                                        <div>
                                            <small style={{ color: 'var(--color-text-muted)' }}>Slot</small>
                                            <div>
                                                <span style={{
                                                    display: 'inline-flex',
                                                    alignItems: 'center',
                                                    gap: '0.3rem',
                                                    background: 'rgba(99,102,241,0.15)',
                                                    color: '#818cf8',
                                                    border: '1px solid rgba(99,102,241,0.35)',
                                                    borderRadius: '6px',
                                                    padding: '0.1rem 0.5rem',
                                                    fontWeight: 600,
                                                    fontSize: '0.85rem',
                                                }}>🅿️ P{booking.slotNumber}</span>
                                            </div>
                                        </div>
                                    )}
                                </div>

                                {booking.status === 0 && ( // Pending
                                    <div className="flex gap-1 mt-2">
                                        <button
                                            className="btn btn-primary"
                                            onClick={() => handleApprove(booking.id)}
                                            disabled={processingId === booking.id}
                                        >
                                            {processingId === booking.id ? 'Processing...' : '✓ Approve'}
                                        </button>
                                        <button
                                            className="btn btn-danger"
                                            onClick={() => setShowRejectModal(booking.id)}
                                            disabled={processingId === booking.id}
                                        >
                                            ✗ Reject
                                        </button>
                                    </div>
                                )}

                                {booking.status === 8 && ( // PendingExtension
                                    <div className="flex gap-1 mt-2">
                                        <button
                                            className="btn btn-primary"
                                            onClick={() => handleApproveExtension(booking.id)}
                                            disabled={processingId === booking.id}
                                        >
                                            {processingId === booking.id ? 'Processing...' : 'Approve Extension'}
                                        </button>
                                        <button
                                            className="btn btn-danger"
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
                    </div>
                )}

                {/* Reject Modal */}
                {showRejectModal && (
                    <div style={{
                        position: 'fixed',
                        top: 0, left: 0, right: 0, bottom: 0,
                        background: 'rgba(0,0,0,0.7)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 1000,
                    }}>
                        <div className="card" style={{ maxWidth: '400px', width: '100%' }}>
                            <h3 className="card-title">Reject Booking</h3>
                            <p className="card-subtitle mb-2">Please provide a reason for rejection (optional):</p>
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
                                    onClick={() => handleReject(showRejectModal)}
                                    disabled={processingId === showRejectModal}
                                >
                                    {processingId === showRejectModal ? 'Rejecting...' : 'Confirm Reject'}
                                </button>
                                <button
                                    className="btn btn-secondary"
                                    onClick={() => {
                                        setShowRejectModal(null);
                                        setRejectReason('');
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
