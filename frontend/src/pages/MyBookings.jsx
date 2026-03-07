import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';
import { handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';
import StripeCheckout from '../components/StripeCheckout';
import BookedSlots from '../components/BookedSlots';

const formatDateTimeLocalInput = (date) => {
    const d = new Date(date);
    const pad = (n) => String(n).padStart(2, '0');
    const yyyy = d.getFullYear();
    const mm = pad(d.getMonth() + 1);
    const dd = pad(d.getDate());
    const hh = pad(d.getHours());
    const min = pad(d.getMinutes());
    return `${yyyy}-${mm}-${dd}T${hh}:${min}`;
};

// A simple countdown timer component to avoid excessive re-renders of the entire list
const CountdownTimer = ({ endDateTime }) => {
    const [timeLeft, setTimeLeft] = useState('');
    const [isEndingSoon, setIsEndingSoon] = useState(false);

    useEffect(() => {
        const calculateTimeLeft = () => {
            const difference = new Date(endDateTime) - new Date();
            if (difference > 0) {
                const hours = Math.floor(difference / (1000 * 60 * 60));
                const minutes = Math.floor((difference / 1000 / 60) % 60);
                const seconds = Math.floor((difference / 1000) % 60);

                setTimeLeft(`${hours}h ${minutes}m ${seconds}s`);
                setIsEndingSoon(difference < 15 * 60 * 1000); // Less than 15 mins
            } else {
                setTimeLeft('Time Ended');
                setIsEndingSoon(true);
            }
        };

        calculateTimeLeft(); // Initial calculation
        const timer = setInterval(calculateTimeLeft, 1000);

        return () => clearInterval(timer);
    }, [endDateTime]);

    return (
        <div style={{
            fontSize: '0.9rem',
            color: isEndingSoon ? 'var(--color-danger)' : 'var(--color-text-secondary)',
            fontWeight: isEndingSoon ? 'bold' : 'normal',
            display: 'flex',
            alignItems: 'center',
            gap: '0.25rem'
        }}>
            ⏱️ {timeLeft}
        </div>
    );
};

const BOOKING_STATUS = [
    'Pending',                // 0
    'Confirmed',              // 1
    'In Progress',            // 2
    'Completed',              // 3
    'Cancelled',              // 4
    'Expired',                // 5
    'Awaiting Payment',       // 6
    'Rejected',               // 7
    'Extension Pending',      // 8
    'Extension Payment Due',  // 9
];
const STATUS_COLORS = {
    0: '#f59e0b', // Pending
    1: '#10b981', // Confirmed
    2: '#6366f1', // InProgress
    3: '#22c55e', // Completed
    4: '#ef4444', // Cancelled
    5: '#9ca3af', // Expired
    6: '#8b5cf6', // Awaiting Payment
    7: '#ef4444', // Rejected
    8: '#f59e0b', // Extension Pending (same amber as Pending)
    9: '#8b5cf6', // Extension Payment Due (same purple as AwaitingPayment)
};

const REFRESH_TRIGGERS = [
    'booking.approved',
    'booking.rejected',
    'payment.completed',
    'booking.checkin',
    'booking.checkout',
    'extension.requested',
    'extension.approved',
    'extension.rejected',
];

export default function MyBookings() {
    const { isVendor } = useAuth();
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

    // Extension Modal State
    const [extensionModalOpen, setExtensionModalOpen] = useState(false);
    const [extendingBooking, setExtendingBooking] = useState(null);
    const [newEndDateTime, setNewEndDateTime] = useState('');
    const [extensionSubmitting, setExtensionSubmitting] = useState(false);
    const [extensionPrice, setExtensionPrice] = useState(null);
    const [calculatingPrice, setCalculatingPrice] = useState(false);
    const [extensionValidationError, setExtensionValidationError] = useState('');
    const [parkingReservations, setParkingReservations] = useState([]);
    const [extensionTotalSpots, setExtensionTotalSpots] = useState(undefined);

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

    const handleOpenExtensionModal = async (booking) => {
        setExtendingBooking(booking);
        // Default to 1 hour after current end time
        const currentEnd = new Date(booking.endDateTime);
        const defaultExtension = new Date(currentEnd.getTime() + 60 * 60 * 1000);
        // Format for datetime-local input (YYYY-MM-DDTHH:mm)
        const formatted = formatDateTimeLocalInput(defaultExtension);
        setNewEndDateTime(formatted);
        setExtensionPrice(null);
        setExtensionValidationError('');
        setParkingReservations([]);
        setExtensionTotalSpots(undefined);
        setExtensionModalOpen(true);
        calculateExtensionPrice(booking, formatted);
        // Fetch active reservations for this parking space
        try {
            const res = await api.getParkingById(booking.parkingSpaceId);
            if (res.success && res.data) {
                setExtensionTotalSpots(res.data.totalSpots);
                if (res.data.activeReservations) {
                    // Exclude the current booking from the list
                    setParkingReservations(
                        res.data.activeReservations.filter(
                            r => r.bookingId !== booking.id && r.id !== booking.id
                        )
                    );
                }
            }
        } catch { /* silently ignore */ }
    };

    const handleCloseExtensionModal = () => {
        setExtensionModalOpen(false);
        setExtendingBooking(null);
        setNewEndDateTime('');
        setExtensionPrice(null);
        setExtensionValidationError('');
    };

    const isValidExtensionDate = (booking, newEnd) => {
        if (!booking || !newEnd) return false;
        const currentEnd = new Date(booking.endDateTime);
        const proposedEnd = new Date(newEnd);
        if (Number.isNaN(currentEnd.getTime()) || Number.isNaN(proposedEnd.getTime())) return false;
        return proposedEnd > currentEnd;
    };

    const calculateExtensionPrice = async (booking, newEnd) => {
        if (!booking || !newEnd) return;
        if (!isValidExtensionDate(booking, newEnd)) {
            setExtensionPrice(null);
            return;
        }
        const newEndUtc = new Date(newEnd).toISOString();
        setCalculatingPrice(true);
        try {
            const res = await api.calculatePrice({
                parkingSpaceId: booking.parkingSpaceId,
                startDateTime: booking.endDateTime, // Calculate for the extra period
                endDateTime: newEndUtc,
                pricingType: booking.pricingType
            });
            if (res.success) {
                setExtensionPrice(res.data);
            }
        } catch (err) {
            console.error('Failed to calculate extension price', err);
        } finally {
            setCalculatingPrice(false);
        }
    };

    const handleExtensionSubmit = async (e) => {
        e.preventDefault();
        if (!extendingBooking) return;
        if (!isValidExtensionDate(extendingBooking, newEndDateTime)) {
            const msg = 'New end time must be greater than current booking end time.';
            setExtensionValidationError(msg);
            showToast.error(msg);
            return;
        }

        setExtensionSubmitting(true);
        try {
            const newEndUtc = new Date(newEndDateTime).toISOString();
            const res = await api.requestExtension(extendingBooking.id, {
                newEndDateTime: newEndUtc
            });

            if (res.success) {
                showToast.success(
                    res.message ||
                    'Extension request submitted! Awaiting owner approval.'
                );
                handleCloseExtensionModal();
                fetchBookings();
            } else {
                showToast.error(res.message || 'Failed to request extension');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to request extension'));
        } finally {
            setExtensionSubmitting(false);
        }
    };

    const handleApproveExtension = async (bookingId) => {
        try {
            const res = await api.approveExtension(bookingId);
            if (res.success) {
                showToast.success('Extension approved! Member will be notified to pay.');
                fetchBookings();
            } else {
                showToast.error(res.message || 'Failed to approve extension');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to approve extension'));
        }
    };

    const handleRejectExtension = async (bookingId) => {
        const reason = window.prompt('Reason for rejecting the extension (optional):');
        // User cancelled the prompt
        if (reason === null) return;
        try {
            const res = await api.rejectExtension(bookingId, reason || 'Rejected by owner');
            if (res.success) {
                showToast.success('Extension request rejected.');
                fetchBookings();
            } else {
                showToast.error(res.message || 'Failed to reject extension');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to reject extension'));
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
                                            {booking.status === 2 && ( // InProgress
                                                <div className="mt-1" style={{ display: 'flex', justifyContent: 'flex-end' }}>
                                                    <CountdownTimer endDateTime={booking.endDateTime} />
                                                </div>
                                            )}
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

                                    <div className="flex gap-1 mt-2">
                                        {booking.status === 6 && ( // AwaitingPayment (initial booking)
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
                                        {booking.status === 9 && ( // AwaitingExtensionPayment
                                            <>
                                                <div style={{ width: '100%', padding: '0.5rem', background: 'rgba(139,92,246,0.1)', borderRadius: 'var(--radius-sm)', marginBottom: '0.5rem', fontSize: '0.85rem', color: '#8b5cf6' }}>
                                                    ⏳ Extension approved — pay ₹{booking.pendingExtensionAmount?.toFixed(2)} to confirm the new end time of {booking.pendingExtensionEndDateTime ? new Date(booking.pendingExtensionEndDateTime).toLocaleString() : ''}
                                                </div>
                                                <button
                                                    className="btn btn-primary"
                                                    onClick={() => handlePayment(booking.id, booking.pendingExtensionAmount)}
                                                    disabled={payingId === booking.id}
                                                >
                                                    {payingId === booking.id ? 'Processing...' : `Pay Extension ₹${booking.pendingExtensionAmount?.toFixed(2)}`}
                                                </button>
                                            </>
                                        )}
                                        {booking.status === 8 && ( // PendingExtension — user waiting for vendor
                                            <div style={{ width: '100%', padding: '0.5rem', background: 'rgba(245,158,11,0.1)', borderRadius: 'var(--radius-sm)', fontSize: '0.85rem', color: '#f59e0b' }}>
                                                ⏳ Extension request pending owner approval — proposed new end: {booking.pendingExtensionEndDateTime ? new Date(booking.pendingExtensionEndDateTime).toLocaleString() : ''}
                                            </div>
                                        )}
                                        {/* Vendor: Approve / Reject extension */}
                                        {isVendor && booking.status === 8 && (
                                            <>
                                                <button
                                                    className="btn btn-primary"
                                                    onClick={() => handleApproveExtension(booking.id)}
                                                >
                                                    ✅ Approve Extension
                                                </button>
                                                <button
                                                    className="btn btn-danger"
                                                    onClick={() => handleRejectExtension(booking.id)}
                                                >
                                                    ❌ Reject Extension
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
                                        {/* Extension Button for Confirmed or InProgress — disabled if there is already a pending extension */}
                                        {[1, 2].includes(booking.status) && (
                                            <button
                                                className="btn btn-outline"
                                                onClick={() => handleOpenExtensionModal(booking)}
                                                disabled={booking.hasPendingExtension}
                                                title={booking.hasPendingExtension ? 'An extension is already pending approval' : ''}
                                            >
                                                ➕ {booking.hasPendingExtension ? 'Extension Pending' : 'Extend'}
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
                                        {[1, 2, 6, 9].includes(booking.status) && booking.latitude && booking.longitude && (
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
            {/* Extension Modal */}
            {extensionModalOpen && extendingBooking && (
                <div className="stripe-modal-overlay">
                    <div className="card stripe-modal" style={{ maxWidth: '400px', width: '90%' }}>
                        <h2 className="card-title mb-2">Extend Booking</h2>
                        <p style={{ fontSize: '0.9rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
                            Current session ends at: <strong>{new Date(extendingBooking.endDateTime).toLocaleString()}</strong>
                        </p>

                        {/* Show other booked slots so user can pick a non-conflicting time */}
                        <BookedSlots reservations={parkingReservations} compact totalSpots={extensionTotalSpots} />

                        <form onSubmit={handleExtensionSubmit}>
                            <div className="form-group">
                                <label className="form-label">New End Time</label>
                                <input
                                    type="datetime-local"
                                    className="form-input"
                                    value={newEndDateTime}
                                    min={formatDateTimeLocalInput(new Date(new Date(extendingBooking.endDateTime).getTime() + 60 * 1000))}
                                    onChange={(e) => {
                                        const value = e.target.value;
                                        setNewEndDateTime(value);
                                        if (!isValidExtensionDate(extendingBooking, value)) {
                                            setExtensionValidationError('New end time must be greater than current booking end time.');
                                            setExtensionPrice(null);
                                            return;
                                        }
                                        setExtensionValidationError('');
                                        calculateExtensionPrice(extendingBooking, value);
                                    }}
                                    required
                                />
                                {extensionValidationError && (
                                    <small style={{ color: 'var(--color-danger)' }}>{extensionValidationError}</small>
                                )}
                            </div>

                            {calculatingPrice ? (
                                <div className="p-2 text-center" style={{ color: 'var(--color-text-muted)' }}>
                                    Calculating price...
                                </div>
                            ) : extensionPrice && (
                                <div className="card mt-2 mb-2" style={{ background: 'rgba(255,255,255,0.05)', border: '1px dashed var(--color-primary)' }}>
                                    <div className="flex-between">
                                        <span>Additional Time:</span>
                                        <strong>{extensionPrice.duration} {extensionPrice.durationUnit}</strong>
                                    </div>
                                    <div className="flex-between mt-1">
                                        <span>Additional Charge:</span>
                                        <strong style={{ color: 'var(--color-primary)', fontSize: '1.2rem' }}>₹{extensionPrice.totalAmount}</strong>
                                    </div>
                                    <p style={{ fontSize: '0.8rem', color: 'var(--color-text-muted)', marginTop: '0.5rem' }}>
                                        * This amount will be added to your total bill.
                                    </p>
                                </div>
                            )}

                            <div className="flex gap-1 mt-3">
                                <button
                                    type="button"
                                    className="btn btn-secondary"
                                    style={{ flex: 1 }}
                                    onClick={handleCloseExtensionModal}
                                    disabled={extensionSubmitting}
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    className="btn btn-primary"
                                    style={{ flex: 1 }}
                                    disabled={extensionSubmitting || !extensionPrice || !!extensionValidationError}
                                >
                                    {extensionSubmitting ? 'Extending...' : 'Confirm Extension'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </>
    );
}
