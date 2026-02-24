import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';
import { handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';
import StripeCheckout from '../components/StripeCheckout';

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

// Notification types that should trigger a refresh of user bookings
const REFRESH_TRIGGERS = [
    'booking.approved',    // Owner approved
    'booking.rejected',    // Owner rejected
    'payment.completed',   // Payment confirmed
    'booking.checkin',     // Checked in
    'booking.checkout'     // Checked out
];

export default function MyBookings() {
    const [bookings, setBookings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState('');
    const [cancellingId, setCancellingId] = useState(null);

    // Review Modal State
    const [reviewModalOpen, setReviewModalOpen] = useState(false);
    const [reviewingBookingId, setReviewingBookingId] = useState(null);
    const [reviewingParkingId, setReviewingParkingId] = useState(null);
    const [reviewRating, setReviewRating] = useState(5);
    const [reviewComment, setReviewComment] = useState('');
    const [reviewSubmitting, setReviewSubmitting] = useState(false);

    const { subscribeToRefresh } = useNotificationContext();

    const fetchBookings = useCallback(async () => {
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
        const unsubscribe = subscribeToRefresh('MyBookings', REFRESH_TRIGGERS, () => {
            // console.log('🔄 MyBookings: Auto-refreshing due to notification');
            fetchBookings();
        });
        return unsubscribe;
    }, [subscribeToRefresh, fetchBookings]);

    const handleCancel = async (id) => {
        if (!window.confirm('Are you sure you want to cancel this booking?')) return;

        setCancellingId(id);

        try {
            const response = await api.cancelBooking(id, 'User requested cancellation');
            if (response.success) {
                showToast.success('Booking cancelled successfully');
                fetchBookings();
            } else {
                showToast.error(response.message || 'Cancel failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to cancel booking'));
        }

        setCancellingId(null);
    };

    const handleCheckIn = async (id) => {
        try {
            const response = await api.checkIn(id);
            if (response.success) {
                showToast.success('Checked in successfully');
                fetchBookings();
            } else {
                showToast.error(response.message || 'Check-in failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to check in'));
        }
    };

    const handleCheckOut = async (id) => {
        try {
            const response = await api.checkOut(id);
            if (response.success) {
                showToast.success('Checked out successfully');
                fetchBookings();
            } else {
                showToast.error(response.message || 'Check-out failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to check out'));
        }
    };

    const [payingId, setPayingId] = useState(null);
    const [stripeConfig, setStripeConfig] = useState({ clientSecret: null, publishableKey: null, bookingId: null });

    // Fetch Stripe publishable key on mount
    useEffect(() => {
        // console.log("[DEBUG] Fetching Stripe Config on mount...");
        api.getStripeConfig().then(res => {
            // console.log("[DEBUG] getStripeConfig response:", res);
            if (res.publishableKey) {
                setStripeConfig(prev => ({ ...prev, publishableKey: res.publishableKey }));
            } else if (res.data?.publishableKey) {
                setStripeConfig(prev => ({ ...prev, publishableKey: res.data.publishableKey }));
            } else {
                console.error("[DEBUG] publishableKey NOT found in response!");
            }
        }).catch((err) => {
            console.error("[DEBUG] Error fetching Stripe Config:", err);
        });
    }, []);

    const handlePayment = async (bookingId, amount) => {
        setPayingId(bookingId);

        try {
            // 1. Create PaymentIntent on backend
            const orderRes = await api.createPaymentOrder(bookingId);
            if (!orderRes.success) {
                throw new Error(orderRes.message || 'Failed to create payment order');
            }

            // orderRes.data is the clientSecret
            setStripeConfig(prev => ({
                ...prev,
                clientSecret: orderRes.data,
                bookingId
            }));

        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to initiate payment'));
            setPayingId(null);
        }
    };

    const handleStripeSuccess = async (paymentIntentId) => {
        try {
            const verifyRes = await api.verifyPayment({
                bookingId: stripeConfig.bookingId,
                razorpayPaymentId: paymentIntentId,
                razorpayOrderId: paymentIntentId,
                razorpaySignature: 'stripe'
            });

            if (verifyRes.success) {
                showToast.success('Payment successful! 🎉');
                fetchBookings();
            } else {
                showToast.error(verifyRes.message || 'Payment verification failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Payment verification failed'));
        } finally {
            setStripeConfig(prev => ({ ...prev, clientSecret: null, bookingId: null }));
            setPayingId(null);
        }
    };

    const handleStripeCancel = () => {
        setStripeConfig(prev => ({ ...prev, clientSecret: null, bookingId: null }));
        setPayingId(null);
    };

    // Review Handlers
    const handleOpenReviewModal = (bookingId, parkingId) => {
        setReviewingBookingId(bookingId);
        setReviewingParkingId(parkingId);
        setReviewRating(5);
        setReviewComment('');
        setReviewModalOpen(true);
    };

    const handleCloseReviewModal = () => {
        setReviewModalOpen(false);
        setReviewingBookingId(null);
        setReviewingParkingId(null);
    };

    const handleSubmitReview = async (e) => {
        e.preventDefault();
        setReviewSubmitting(true);
        try {
            const res = await api.createReview({
                parkingSpaceId: reviewingParkingId,
                bookingId: reviewingBookingId,
                rating: reviewRating,
                title: 'Review',
                comment: reviewComment
            });

            if (res.success) {
                showToast.success('Review submitted successfully!');
                handleCloseReviewModal();
                fetchBookings(); // Optionally refresh if we want to add an indicator that a review exists
            } else {
                showToast.error(res.message || 'Failed to submit review');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to submit review'));
        } finally {
            setReviewSubmitting(false);
        }
    };

    return (
        <>
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



                    {loading ? (
                        <div className="grid" style={{ gap: '1rem' }}>
                            {[1, 2, 3].map(n => (
                                <div key={n} className="skeleton-card" style={{ minHeight: '120px' }} />
                            ))}
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
                                <div key={booking.id} className="card hover-card">
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
                                                    onClick={() => handlePayment(booking.id, booking.totalAmount)}
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
                                        {/* Navigation Button for all active bookings */}
                                        {[1, 2, 6].includes(booking.status) && booking.latitude && booking.longitude && (
                                            <a
                                                href={`https://www.google.com/maps/dir/?api=1&destination=${parseFloat(booking.latitude)},${parseFloat(booking.longitude)}`}
                                                target="_blank"
                                                rel="noopener noreferrer"
                                                className="btn btn-outline"
                                            >
                                                🧭 Get Directions
                                            </a>
                                        )}
                                        {booking.status === 3 && ( // Completed
                                            <button
                                                className="btn btn-outline"
                                                onClick={() => handleOpenReviewModal(booking.id, booking.parkingSpaceId)}
                                            >
                                                ⭐ Leave Review
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

            {/* Stripe Checkout Modal */}
            {stripeConfig.clientSecret && stripeConfig.publishableKey && (
                <div className="stripe-modal-overlay">
                    <div className="card stripe-modal">
                        <h3 className="card-title mb-2">💳 Complete Payment</h3>
                        <StripeCheckout
                            clientSecret={stripeConfig.clientSecret}
                            publishableKey={stripeConfig.publishableKey}
                            bookingId={stripeConfig.bookingId}
                            onSuccess={handleStripeSuccess}
                            onCancel={handleStripeCancel}
                        />
                    </div>
                </div>
            )}

            {/* Review Modal */}
            {reviewModalOpen && (
                <div className="stripe-modal-overlay">
                    <div className="card stripe-modal" style={{ maxWidth: '400px', width: '90%' }}>
                        <h3 className="card-title mb-2">⭐ Leave a Review</h3>
                        <form onSubmit={handleSubmitReview}>
                            <div className="form-group text-center">
                                <div style={{ fontSize: '2.5rem', cursor: 'pointer', display: 'flex', justifyContent: 'center', gap: '0.5rem' }}>
                                    {[1, 2, 3, 4, 5].map(star => (
                                        <span
                                            key={star}
                                            onClick={() => setReviewRating(star)}
                                            style={{
                                                color: star <= reviewRating ? '#f59e0b' : 'rgba(255,255,255,0.1)',
                                                transition: 'color 0.2s, transform 0.1s',
                                            }}
                                            onMouseEnter={(e) => e.currentTarget.style.transform = 'scale(1.2)'}
                                            onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                                        >
                                            ★
                                        </span>
                                    ))}
                                </div>
                                <div style={{ fontSize: '0.9rem', color: 'var(--color-text-secondary)', marginTop: '0.5rem' }}>
                                    {reviewRating} out of 5 stars
                                </div>
                            </div>

                            <div className="form-group">
                                <label className="form-label">Comment (Optional)</label>
                                <textarea
                                    className="form-input"
                                    rows="3"
                                    placeholder="Tell us about your experience..."
                                    value={reviewComment}
                                    onChange={(e) => setReviewComment(e.target.value)}
                                    style={{ resize: 'vertical', minHeight: '80px' }}
                                ></textarea>
                            </div>

                            <div className="flex gap-1 mt-2">
                                <button
                                    type="button"
                                    className="btn btn-secondary"
                                    style={{ flex: 1 }}
                                    onClick={handleCloseReviewModal}
                                    disabled={reviewSubmitting}
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    className="btn btn-primary"
                                    style={{ flex: 1 }}
                                    disabled={reviewSubmitting}
                                >
                                    {reviewSubmitting ? 'Submitting...' : 'Submit Review'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </>
    );
}
