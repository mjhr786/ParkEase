import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import api from '../services/api';

const BOOKING_STATUS = ['Pending', 'Confirmed', 'InProgress', 'Completed', 'Cancelled', 'Expired', 'Awaiting Payment'];
const STATUS_COLORS = {
    0: '#f59e0b', // Pending
    1: '#10b981', // Confirmed
    2: '#6366f1', // InProgress
    3: '#22c55e', // Completed
    4: '#ef4444', // Cancelled
    5: '#9ca3af', // Expired
    6: '#8b5cf6', // Awaiting Payment (purple)
};

export default function VendorBookings() {
    const [bookings, setBookings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState('0'); // Default to Pending
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [processingId, setProcessingId] = useState(null);
    const [rejectReason, setRejectReason] = useState('');
    const [showRejectModal, setShowRejectModal] = useState(null);

    useEffect(() => {
        fetchBookings();
    }, [filter]);

    const fetchBookings = async () => {
        setLoading(true);
        try {
            const params = filter ? { status: filter } : {};
            const response = await api.getVendorBookings(params);
            if (response.success && response.data) {
                const bookingsData = Array.isArray(response.data)
                    ? response.data
                    : (response.data.bookings || response.data.items || []);
                setBookings(bookingsData);
            } else {
                setBookings([]);
            }
        } catch (err) {
            console.error('Fetch vendor bookings error:', err);
            setBookings([]);
        }
        setLoading(false);
    };

    const handleApprove = async (id) => {
        setProcessingId(id);
        setError('');
        setSuccess('');

        try {
            const response = await api.approveBooking(id);
            if (response.success) {
                setSuccess('Booking approved successfully!');
                fetchBookings();
            } else {
                setError(response.message || 'Failed to approve booking');
            }
        } catch (err) {
            setError(err.message);
        }

        setProcessingId(null);
    };

    const handleReject = async (id) => {
        setProcessingId(id);
        setError('');
        setSuccess('');

        try {
            const response = await api.rejectBooking(id, rejectReason);
            if (response.success) {
                setSuccess('Booking rejected');
                setShowRejectModal(null);
                setRejectReason('');
                fetchBookings();
            } else {
                setError(response.message || 'Failed to reject booking');
            }
        } catch (err) {
            setError(err.message);
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
                        <option value="">All Bookings</option>
                        {BOOKING_STATUS.map((status, i) => (
                            <option key={i} value={i}>{status}</option>
                        ))}
                    </select>
                </div>

                {error && <div className="alert alert-error">{error}</div>}
                {success && <div className="alert alert-success">{success}</div>}

                {loading ? (
                    <div className="loading">
                        <div className="spinner"></div>
                    </div>
                ) : bookings.length === 0 ? (
                    <div className="empty-state">
                        <div className="empty-icon">📋</div>
                        <h3>No booking requests</h3>
                        <p>{filter === '0' ? 'No pending requests to review' : 'No bookings found with this status'}</p>
                    </div>
                ) : (
                    <div className="grid" style={{ gap: '1rem' }}>
                        {bookings.map(booking => (
                            <div key={booking.id} className="card">
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
                                        <div>{booking.vehicleNumber || 'Not specified'}</div>
                                    </div>
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

                                {booking.status !== 0 && (
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
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
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
