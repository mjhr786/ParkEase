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

export default function MyBookings() {
    const [bookings, setBookings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState('');
    const [error, setError] = useState('');
    const [cancellingId, setCancellingId] = useState(null);

    useEffect(() => {
        fetchBookings();
    }, [filter]);

    const fetchBookings = async () => {
        setLoading(true);
        try {
            const params = filter ? { status: filter } : {};
            const response = await api.getMyBookings(params);
            if (response.success && response.data) {
                // Handle both array and paginated object responses
                const bookingsData = Array.isArray(response.data)
                    ? response.data
                    : (response.data.bookings || response.data.items || []);
                setBookings(bookingsData);
            } else {
                setBookings([]);
            }
        } catch (err) {
            console.error('Fetch bookings error:', err);
            setBookings([]);
        }
        setLoading(false);
    };

    const handleCancel = async (id) => {
        if (!window.confirm('Are you sure you want to cancel this booking?')) return;

        setCancellingId(id);
        setError('');

        try {
            const response = await api.cancelBooking(id, 'User requested cancellation');
            if (response.success) {
                fetchBookings();
            } else {
                setError(response.message || 'Failed to cancel booking');
            }
        } catch (err) {
            setError(err.message);
        }

        setCancellingId(null);
    };

    const handleCheckIn = async (id) => {
        try {
            const response = await api.checkIn(id);
            if (response.success) {
                fetchBookings();
            } else {
                setError(response.message || 'Check-in failed');
            }
        } catch (err) {
            setError(err.message);
        }
    };

    const handleCheckOut = async (id) => {
        try {
            const response = await api.checkOut(id);
            if (response.success) {
                fetchBookings();
            } else {
                setError(response.message || 'Check-out failed');
            }
        } catch (err) {
            setError(err.message);
        }
    };

    const [payingId, setPayingId] = useState(null);

    const handlePayment = async (bookingId) => {
        setPayingId(bookingId);
        setError('');

        try {
            const response = await api.processPayment({
                bookingId: bookingId,
                paymentMethod: 0, // Default to Credit Card
            });

            if (response.success && response.data?.success) {
                fetchBookings();
            } else {
                setError(response.data?.message || response.message || 'Payment failed');
            }
        } catch (err) {
            setError(err.message || 'Payment failed');
        }

        setPayingId(null);
    };

    return (
        <div className="page">
            <div className="container">
                <div className="flex-between mb-3">
                    <h1>My Bookings</h1>
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

                {loading ? (
                    <div className="loading">
                        <div className="spinner"></div>
                    </div>
                ) : bookings.length === 0 ? (
                    <div className="empty-state">
                        <div className="empty-icon">📋</div>
                        <h3>No bookings found</h3>
                        <p>Start by searching for parking spaces</p>
                        <Link to="/search" className="btn btn-primary mt-2">
                            Find Parking
                        </Link>
                    </div>
                ) : (
                    <div className="grid" style={{ gap: '1rem' }}>
                        {bookings.map(booking => (
                            <div key={booking.id} className="card">
                                <div className="flex-between">
                                    <div>
                                        <h3 className="card-title">{booking.parkingSpaceTitle}</h3>
                                        <div className="parking-location">
                                            📍 {booking.parkingSpaceAddress}
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

                                <div className="flex gap-1 mt-2">
                                    {booking.status === 6 && ( // AwaitingPayment
                                        <>
                                            <button
                                                className="btn btn-primary"
                                                onClick={() => handlePayment(booking.id)}
                                                disabled={payingId === booking.id}
                                            >
                                                {payingId === booking.id ? 'Processing...' : `Pay ₹${booking.totalAmount}`}
                                            </button>
                                            <button
                                                className="btn btn-danger"
                                                onClick={() => handleCancel(booking.id)}
                                                disabled={cancellingId === booking.id}
                                            >
                                                {cancellingId === booking.id ? 'Cancelling...' : 'Cancel'}
                                            </button>
                                        </>
                                    )}
                                    {booking.status === 1 && ( // Confirmed (paid)
                                        <>
                                            <button
                                                className="btn btn-primary"
                                                onClick={() => handleCheckIn(booking.id)}
                                            >
                                                Check In
                                            </button>
                                            <button
                                                className="btn btn-danger"
                                                onClick={() => handleCancel(booking.id)}
                                                disabled={cancellingId === booking.id}
                                            >
                                                {cancellingId === booking.id ? 'Cancelling...' : 'Cancel'}
                                            </button>
                                        </>
                                    )}
                                    {booking.status === 2 && ( // InProgress
                                        <button
                                            className="btn btn-primary"
                                            onClick={() => handleCheckOut(booking.id)}
                                        >
                                            Check Out
                                        </button>
                                    )}
                                    {booking.status === 0 && ( // Pending
                                        <button
                                            className="btn btn-danger"
                                            onClick={() => handleCancel(booking.id)}
                                            disabled={cancellingId === booking.id}
                                        >
                                            {cancellingId === booking.id ? 'Cancelling...' : 'Cancel'}
                                        </button>
                                    )}
                                    <Link to={`/parking/${booking.parkingSpaceId}`} className="btn btn-secondary">
                                        View Parking
                                    </Link>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
