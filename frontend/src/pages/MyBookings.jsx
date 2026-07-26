import { useState, useEffect, useCallback, useRef } from 'react';
import { Link, useSearchParams } from 'react-router-dom';

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

/** PricingType: 0=Hourly, 1=Daily, 2=Weekly, 3=Monthly — day-based ignore clock times in display. */
const isDayBasedPricing = (pricingType) => Number(pricingType) > 0;

const PRICING_TYPES = [
    { value: 0, label: 'Hourly' },
    { value: 1, label: 'Daily' },
    { value: 2, label: 'Weekly' },
    { value: 3, label: 'Monthly' },
];

const toDateOnly = (value) => {
    if (!value) return '';
    if (value instanceof Date) {
        const pad = (n) => String(n).padStart(2, '0');
        return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
    }
    return String(value).slice(0, 10);
};

/** Default new end based on pricing unit after the current booking end. */
const defaultExtensionEnd = (currentEnd, pricingType) => {
    const end = new Date(currentEnd);
    const type = Number(pricingType);
    if (type === 1) {
        // Daily: end of next calendar day
        const d = new Date(end);
        d.setDate(d.getDate() + 1);
        d.setHours(23, 59, 0, 0);
        return d;
    }
    if (type === 2) {
        const d = new Date(end);
        d.setDate(d.getDate() + 7);
        d.setHours(23, 59, 0, 0);
        return d;
    }
    if (type === 3) {
        const d = new Date(end);
        d.setDate(d.getDate() + 30);
        d.setHours(23, 59, 0, 0);
        return d;
    }
    // Hourly: +1 hour
    return new Date(end.getTime() + 60 * 60 * 1000);
};

/**
 * Resolve extension end ISO for quote/API.
 * Day-based: end of selected calendar day; hourly: exact datetime-local.
 */
const resolveExtensionEndIso = (newEndValue, pricingType) => {
    if (!newEndValue) return null;
    if (isDayBasedPricing(pricingType)) {
        const dateOnly = toDateOnly(newEndValue);
        return new Date(`${dateOnly}T23:59:59`).toISOString();
    }
    return new Date(newEndValue).toISOString();
};

const formatBookingRangeValue = (dateTime, pricingType) => {
    const d = new Date(dateTime);
    if (Number.isNaN(d.getTime())) return '—';
    if (isDayBasedPricing(pricingType)) {
        return d.toLocaleDateString(undefined, {
            year: 'numeric',
            month: 'numeric',
            day: 'numeric',
        });
    }
    return d.toLocaleString();
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
    0: 'var(--color-warning)', // Pending
    1: 'var(--color-success)', // Confirmed
    2: 'var(--color-primary)', // InProgress
    3: 'var(--color-success)', // Completed
    4: 'var(--color-error)', // Cancelled
    5: 'var(--color-text-muted)', // Expired
    6: 'var(--color-secondary)', // Awaiting Payment
    7: 'var(--color-error)', // Rejected
    8: 'var(--color-warning)', // Extension Pending (same amber as Pending)
    9: 'var(--color-secondary)', // Extension Payment Due (same purple as AwaitingPayment)
};

const REFRESH_TRIGGERS = ['booking.approved', 'booking.rejected', 'payment.completed', 'extension.requested', 'extension.approved', 'extension.rejected'];

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

    // Extension Modal State
    const [extensionModalOpen, setExtensionModalOpen] = useState(false);
    const [extendingBooking, setExtendingBooking] = useState(null);
    const [newEndDateTime, setNewEndDateTime] = useState('');
    const [extensionPricingType, setExtensionPricingType] = useState(0);
    const [extensionSubmitting, setExtensionSubmitting] = useState(false);
    const [extensionPrice, setExtensionPrice] = useState(null);
    const [calculatingPrice, setCalculatingPrice] = useState(false);
    const [extensionValidationError, setExtensionValidationError] = useState('');
    const [parkingReservations, setParkingReservations] = useState([]);
    const [extensionTotalSpots, setExtensionTotalSpots] = useState(undefined);

    // Digital access pass (QR)
    const [accessPassOpen, setAccessPassOpen] = useState(false);
    const [accessPass, setAccessPass] = useState(null);
    const [accessPassLoading, setAccessPassLoading] = useState(false);

    const { subscribeToRefresh } = useNotificationContext();
    const [searchParams, setSearchParams] = useSearchParams();
    const deepLinkHandled = useRef(false);

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
                return bookingsData;
            } else {
                setBookings([]);
                return [];
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to load bookings'));
            setBookings([]);
            return [];
        } finally {
            setLoading(false);
        }
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

    const handleShowAccessPass = async (bookingId) => {
        setAccessPassLoading(true);
        setAccessPass(null);
        try {
            const response = await api.getAccessPass(bookingId);
            if (response.success && response.data) {
                setAccessPass(response.data);
                setAccessPassOpen(true);
            } else {
                showToast.error(response.message || 'Access pass not available');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to load access pass'));
        }
        setAccessPassLoading(false);
    };

    const handleAddAppleWallet = async () => {
        if (!accessPass?.bookingId) return;
        try {
            const { blob, fileName } = await api.downloadAppleWalletPass(accessPass.bookingId);
            const objectUrl = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = objectUrl;
            a.download = fileName || 'ParkEase.pkpass';
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(objectUrl);
            if (accessPass.appleWalletIsSigned) {
                showToast.success('Apple Wallet pass downloaded');
            } else {
                showToast.success('Pass package downloaded (unsigned — for dev; iOS needs signing certs)');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Apple Wallet download failed'));
        }
    };

    const handleAddGoogleWallet = async () => {
        if (!accessPass?.bookingId) return;
        try {
            const response = await api.getGoogleWalletSaveLink(accessPass.bookingId);
            if (response.success && response.data?.saveUrl) {
                window.open(response.data.saveUrl, '_blank', 'noopener,noreferrer');
            } else {
                showToast.error(response.message || response.data?.message || 'Google Wallet not available');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Google Wallet link failed'));
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

    const handlePayment = async (bookingId, amount, { payOverstayFee = false } = {}) => {
        setPayingId(bookingId);

        try {
            // 1. Create PaymentIntent on backend
            const orderRes = await api.createPaymentOrder(bookingId, { payOverstayFee });
            if (!orderRes.success) {
                throw new Error(orderRes.message || 'Failed to create payment order');
            }

            // orderRes.data is the clientSecret
            setStripeConfig(prev => ({
                ...prev,
                clientSecret: orderRes.data,
                bookingId,
                payOverstayFee: !!payOverstayFee,
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
                showToast.success(
                    stripeConfig.payOverstayFee
                        ? 'Overstay fee paid successfully! 🎉'
                        : 'Payment successful! 🎉'
                );
                fetchBookings();
            } else {
                showToast.error(verifyRes.message || 'Payment verification failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Payment verification failed'));
        } finally {
            setStripeConfig(prev => ({ ...prev, clientSecret: null, bookingId: null, payOverstayFee: false }));
            setPayingId(null);
        }
    };

    const handleStripeCancel = () => {
        setStripeConfig(prev => ({ ...prev, clientSecret: null, bookingId: null, payOverstayFee: false }));
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
        const initialPricingType = Number(booking.pricingType ?? 0);
        setExtensionPricingType(initialPricingType);
        const defaultExtension = defaultExtensionEnd(booking.endDateTime, initialPricingType);
        const formatted = isDayBasedPricing(initialPricingType)
            ? toDateOnly(defaultExtension)
            : formatDateTimeLocalInput(defaultExtension);
        setNewEndDateTime(formatted);
        setExtensionPrice(null);
        setExtensionValidationError('');
        setParkingReservations([]);
        setExtensionTotalSpots(undefined);
        setExtensionModalOpen(true);
        calculateExtensionPrice(booking, formatted, initialPricingType);
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
        setExtensionPricingType(0);
        setExtensionPrice(null);
        setExtensionValidationError('');
    };

    // Deep-link from overstay notifications: ?action=extend|checkout&bookingId=
    useEffect(() => {
        if (deepLinkHandled.current || loading || bookings.length === 0) return;

        const action = searchParams.get('action');
        const bookingId = searchParams.get('bookingId');
        if (!action && !bookingId) return;

        deepLinkHandled.current = true;
        const booking = bookingId
            ? bookings.find((b) => String(b.id) === String(bookingId))
            : null;

        if (!booking && bookingId) {
            showToast.error('Booking not found in your list. It may have already been completed.');
            setSearchParams({}, { replace: true });
            return;
        }

        if (action === 'extend' && booking) {
            if (booking.status === 2 || booking.status === 'InProgress' || booking.status === 1 || booking.status === 'Confirmed') {
                handleOpenExtensionModal(booking);
                showToast.success('Extend your booking to keep parking.');
            } else {
                showToast.error('This booking cannot be extended right now.');
            }
        } else if (action === 'checkout' && booking) {
            if (booking.status === 2 || booking.status === 'InProgress') {
                // Confirm then check out
                if (window.confirm('Check out of this parking session now?')) {
                    handleCheckOut(booking.id);
                }
            } else {
                showToast.error('This booking is not checked in, so check-out is not available.');
            }
        }

        setSearchParams({}, { replace: true });
        // eslint-disable-next-line react-hooks/exhaustive-deps -- one-shot deep link after first load
    }, [bookings, loading, searchParams]);

    const isValidExtensionDate = (booking, newEnd, pricingType = 0) => {
        if (!booking || !newEnd) return false;
        const currentEnd = new Date(booking.endDateTime);
        const proposedIso = resolveExtensionEndIso(newEnd, pricingType);
        if (!proposedIso) return false;
        const proposedEnd = new Date(proposedIso);
        if (Number.isNaN(currentEnd.getTime()) || Number.isNaN(proposedEnd.getTime())) return false;
        return proposedEnd > currentEnd;
    };

    const calculateExtensionPrice = async (booking, newEnd, pricingType) => {
        if (!booking || !newEnd) return;
        const type = pricingType ?? extensionPricingType;
        if (!isValidExtensionDate(booking, newEnd, type)) {
            setExtensionPrice(null);
            return;
        }
        const newEndUtc = resolveExtensionEndIso(newEnd, type);
        setCalculatingPrice(true);
        try {
            const res = await api.calculatePrice({
                parkingSpaceId: booking.parkingSpaceId,
                startDateTime: booking.endDateTime, // Calculate for the extra period
                endDateTime: newEndUtc,
                pricingType: type,
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

    const handleExtensionPricingTypeChange = (nextType) => {
        const type = parseInt(nextType, 10);
        setExtensionPricingType(type);
        if (!extendingBooking) return;
        const defaultEnd = defaultExtensionEnd(extendingBooking.endDateTime, type);
        const formatted = isDayBasedPricing(type)
            ? toDateOnly(defaultEnd)
            : formatDateTimeLocalInput(defaultEnd);
        setNewEndDateTime(formatted);
        setExtensionValidationError('');
        calculateExtensionPrice(extendingBooking, formatted, type);
    };

    const handleExtensionSubmit = async (e) => {
        e.preventDefault();
        if (!extendingBooking) return;
        if (!isValidExtensionDate(extendingBooking, newEndDateTime, extensionPricingType)) {
            const msg = isDayBasedPricing(extensionPricingType)
                ? 'New end date must be after the current booking end.'
                : 'New end time must be greater than current booking end time.';
            setExtensionValidationError(msg);
            showToast.error(msg);
            return;
        }

        setExtensionSubmitting(true);
        try {
            const newEndUtc = resolveExtensionEndIso(newEndDateTime, extensionPricingType);
            const res = await api.requestExtension(extendingBooking.id, {
                newEndDateTime: newEndUtc,
                pricingType: extensionPricingType,
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
                                            {Number(booking.ancillarySubtotal) > 0 && (
                                                <div style={{ marginTop: '0.35rem', fontSize: '0.8rem', color: 'var(--color-secondary)' }}>
                                                    + add-ons ₹{booking.ancillarySubtotal}
                                                </div>
                                            )}
                                            {booking.status === 2 && ( // InProgress
                                                <div className="mt-1" style={{ display: 'flex', justifyContent: 'flex-end' }}>
                                                    <CountdownTimer endDateTime={booking.endDateTime} />
                                                </div>
                                            )}
                                        </div>
                                    </div>

                                    {Array.isArray(booking.ancillaryLines) && booking.ancillaryLines.length > 0 && (
                                        <div style={{
                                            marginTop: '0.75rem',
                                            padding: '0.6rem 0.75rem',
                                            background: 'rgba(244, 114, 182, 0.08)',
                                            borderRadius: 'var(--radius-sm)',
                                            border: '1px solid rgba(244, 114, 182, 0.25)',
                                            fontSize: '0.85rem',
                                        }}>
                                            <strong style={{ color: 'var(--color-secondary)' }}>Add-ons</strong>
                                            <ul style={{ listStyle: 'none', padding: 0, margin: '0.35rem 0 0' }}>
                                                {booking.ancillaryLines.map((line, idx) => (
                                                    <li key={line.id || idx} style={{ display: 'flex', justifyContent: 'space-between', gap: '0.5rem' }}>
                                                        <span>
                                                            {line.snapshotName}
                                                            {line.quantity > 1 ? ` ×${line.quantity}` : ''}
                                                        </span>
                                                        <span>₹{line.lineTotal ?? line.unitPrice}</span>
                                                    </li>
                                                ))}
                                            </ul>
                                        </div>
                                    )}

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
                                                        color: 'var(--color-accent-light)',
                                                        border: '1px solid rgba(99,102,241,0.35)',
                                                        borderRadius: '6px',
                                                        padding: '0.1rem 0.5rem',
                                                        fontWeight: 600,
                                                        fontSize: '0.85rem',
                                                    }}>🅿️ P{booking.slotNumber}</span>
                                                </div>
                                            </div>
                                        )}
                                        {(booking.bayLabel || booking.facilityLevel || booking.facilityZone || booking.isBayGuidanceEnabled) && (
                                            <div style={{ gridColumn: '1 / -1' }}>
                                                <small style={{ color: 'var(--color-text-muted)' }}>Find your bay</small>
                                                <div style={{
                                                    marginTop: '0.25rem',
                                                    padding: '0.6rem 0.75rem',
                                                    background: 'rgba(59,130,246,0.1)',
                                                    border: '1px solid rgba(59,130,246,0.3)',
                                                    borderRadius: 'var(--radius-sm)',
                                                    fontSize: '0.9rem',
                                                }}>
                                                    <strong>
                                                        {[booking.facilityLevel, booking.facilityZone, booking.bayLabel || (booking.slotNumber ? `B-${booking.slotNumber}` : null)]
                                                            .filter(Boolean)
                                                            .join(' · ') || 'Bay assignment pending'}
                                                    </strong>
                                                    {booking.indoorGuidanceNotes && (
                                                        <div style={{ marginTop: '0.35rem', color: 'var(--color-text-muted)', fontSize: '0.85rem' }}>
                                                            {booking.indoorGuidanceNotes}
                                                        </div>
                                                    )}
                                                </div>
                                            </div>
                                        )}
                                    </div>

                                    <div className="flex gap-1 mt-2">
                                        {(booking.status === 6 || booking.status === 'AwaitingPayment') && ( // AwaitingPayment (initial booking)
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
                                        {(booking.status === 9 || booking.status === 'AwaitingExtensionPayment') && ( // AwaitingExtensionPayment
                                            <>
                                                <div style={{ width: '100%', padding: '0.5rem', background: 'rgba(139,92,246,0.1)', borderRadius: 'var(--radius-sm)', marginBottom: '0.5rem', fontSize: '0.85rem', color: 'var(--color-secondary)' }}>
                                                    ⏳ Extension approved — pay ₹{Number(booking.pendingExtensionAmount || 0).toFixed(2)} to confirm the new end time of {booking.pendingExtensionEndDateTime ? new Date(booking.pendingExtensionEndDateTime).toLocaleString() : ''}
                                                </div>
                                                <button
                                                    className="btn btn-primary"
                                                    onClick={() => handlePayment(booking.id, booking.pendingExtensionAmount)}
                                                    disabled={payingId === booking.id}
                                                >
                                                    {payingId === booking.id ? 'Processing...' : `Pay ₹${Number(booking.pendingExtensionAmount || 0).toFixed(2)}`}
                                                </button>
                                            </>
                                        )}
                                        {(booking.status === 8 || booking.status === 'PendingExtension') && ( // PendingExtension — user waiting for vendor
                                            <div style={{ width: '100%', padding: '0.5rem', background: 'rgba(245,158,11,0.1)', borderRadius: 'var(--radius-sm)', fontSize: '0.85rem', color: 'var(--color-warning)' }}>
                                                ⏳ Extension request pending owner approval — proposed new end: {booking.pendingExtensionEndDateTime ? new Date(booking.pendingExtensionEndDateTime).toLocaleString() : ''}
                                                {Number(booking.pendingExtensionAmount) > 0 && (
                                                    <> · amount due if approved: ₹{Number(booking.pendingExtensionAmount).toFixed(2)}</>
                                                )}
                                            </div>
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
                                                    className="btn btn-outline"
                                                    onClick={() => handleShowAccessPass(booking.id)}
                                                    disabled={accessPassLoading}
                                                >
                                                    📱 Access pass
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
                                        {Number(booking.overstayFeeOutstanding) > 0 && (
                                            <div style={{
                                                width: '100%',
                                                padding: '0.5rem',
                                                background: 'rgba(239,68,68,0.12)',
                                                borderRadius: 'var(--radius-sm)',
                                                marginBottom: '0.5rem',
                                                fontSize: '0.85rem',
                                                color: 'var(--color-error)',
                                            }}>
                                                ⏱ Overstay fee due: ₹{Number(booking.overstayFeeOutstanding).toFixed(2)}
                                                {Number(booking.overstayFeeAmount) > Number(booking.overstayFeePaidAmount || 0) && (
                                                    <span style={{ color: 'var(--color-text-secondary)' }}>
                                                        {' '}(assessed ₹{Number(booking.overstayFeeAmount).toFixed(2)}
                                                        {Number(booking.overstayFeePaidAmount) > 0
                                                            ? `, paid ₹${Number(booking.overstayFeePaidAmount).toFixed(2)}`
                                                            : ''})
                                                    </span>
                                                )}
                                                <div style={{ marginTop: '0.4rem' }}>
                                                    <button
                                                        className="btn btn-primary"
                                                        onClick={() => handlePayment(booking.id, booking.overstayFeeOutstanding, { payOverstayFee: true })}
                                                        disabled={payingId === booking.id}
                                                    >
                                                        {payingId === booking.id
                                                            ? 'Processing...'
                                                            : `Pay overstay ₹${Number(booking.overstayFeeOutstanding).toFixed(2)}`}
                                                    </button>
                                                </div>
                                            </div>
                                        )}
                                        {booking.status === 2 && ( // InProgress
                                            <>
                                                <button
                                                    className="btn btn-primary"
                                                    onClick={() => handleCheckOut(booking.id)}
                                                >
                                                    Check Out
                                                </button>
                                                <button
                                                    className="btn btn-outline"
                                                    onClick={() => handleShowAccessPass(booking.id)}
                                                    disabled={accessPassLoading}
                                                >
                                                    📱 Access pass
                                                </button>
                                            </>
                                        )}
                                        {booking.isValetEnabled && [1, 2, 8, 9].includes(booking.status) && (
                                            <div style={{
                                                width: '100%',
                                                marginTop: '0.35rem',
                                                padding: '0.55rem 0.7rem',
                                                background: 'rgba(168,85,247,0.1)',
                                                border: '1px solid rgba(168,85,247,0.3)',
                                                borderRadius: 'var(--radius-sm)',
                                                fontSize: '0.85rem',
                                            }}>
                                                <div style={{ marginBottom: '0.35rem', color: 'var(--color-secondary)', fontWeight: 600 }}>
                                                    🚗 Valet
                                                    {booking.valetStatus === 1 && ' · Requested'}
                                                    {booking.valetStatus === 2 && ' · Retrieving'}
                                                    {booking.valetStatus === 3 && ' · Ready for pickup'}
                                                    {booking.valetStatus === 4 && ' · Completed'}
                                                    {booking.valetStatus === 5 && ' · Cancelled'}
                                                    {booking.valetTargetReadyAt && booking.valetStatus > 0 && booking.valetStatus < 4 && (
                                                        <span style={{ fontWeight: 400, color: 'var(--color-text-muted)' }}>
                                                            {' '}· target {new Date(booking.valetTargetReadyAt).toLocaleTimeString()}
                                                        </span>
                                                    )}
                                                </div>
                                                {(!booking.valetStatus || booking.valetStatus === 0 || booking.valetStatus === 5) && (
                                                    <button
                                                        className="btn btn-outline"
                                                        style={{ fontSize: '0.85rem' }}
                                                        onClick={async () => {
                                                            try {
                                                                const res = await api.requestValet(booking.id, { leadMinutes: 10 });
                                                                if (res.success) {
                                                                    showToast.success(res.message || 'Valet requested');
                                                                    fetchBookings();
                                                                } else {
                                                                    showToast.error(res.message || 'Failed to request valet');
                                                                }
                                                            } catch (err) {
                                                                showToast.error(handleApiError(err, 'Failed to request valet'));
                                                            }
                                                        }}
                                                    >
                                                        Request vehicle (~10 min)
                                                    </button>
                                                )}
                                                {[1, 2, 3].includes(booking.valetStatus) && (
                                                    <button
                                                        className="btn btn-outline"
                                                        style={{ fontSize: '0.85rem' }}
                                                        onClick={async () => {
                                                            try {
                                                                const res = await api.cancelValet(booking.id);
                                                                if (res.success) {
                                                                    showToast.success('Valet cancelled');
                                                                    fetchBookings();
                                                                } else {
                                                                    showToast.error(res.message || 'Failed to cancel');
                                                                }
                                                            } catch (err) {
                                                                showToast.error(handleApiError(err, 'Failed to cancel valet'));
                                                            }
                                                        }}
                                                    >
                                                        Cancel valet
                                                    </button>
                                                )}
                                            </div>
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
                                        {/* Navigation Button for all active bookings.
                                            Use Boolean(...) so a missing/zero lat/lng does not render as "0"
                                            (React renders the number 0 from short-circuit && expressions). */}
                                        {Boolean(
                                            [1, 2, 6, 9].includes(booking.status) &&
                                            booking.latitude &&
                                            booking.longitude
                                        ) && (
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

            {/* Digital Access Pass Modal */}
            {accessPassOpen && accessPass && (
                <div className="stripe-modal-overlay" onClick={() => setAccessPassOpen(false)}>
                    <div
                        className="card stripe-modal"
                        style={{ maxWidth: '400px', width: '90%', textAlign: 'center' }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 className="card-title mb-1">📱 Digital Access Pass</h3>
                        <p className="card-subtitle" style={{ marginBottom: '0.75rem' }}>
                            {accessPass.parkingSpaceTitle}
                        </p>
                        <div style={{
                            display: 'inline-block',
                            padding: '0.75rem',
                            background: '#fff',
                            borderRadius: '12px',
                            marginBottom: '0.75rem',
                        }}>
                            <img
                                src={accessPass.qrImageUrl}
                                alt="Access QR code"
                                width={240}
                                height={240}
                                style={{ display: 'block' }}
                            />
                        </div>
                        <div style={{
                            fontSize: '0.8rem',
                            color: accessPass.isValidNow ? 'var(--color-success)' : 'var(--color-warning)',
                            fontWeight: 600,
                            marginBottom: '0.5rem',
                        }}>
                            {accessPass.isValidNow ? 'Valid now for gate access' : 'Outside access window or inactive'}
                        </div>
                        <div style={{ fontSize: '0.85rem', color: 'var(--color-text-secondary)', marginBottom: '0.35rem' }}>
                            Ref: {accessPass.bookingReference || '—'}
                        </div>
                        <div style={{ fontSize: '0.8rem', color: 'var(--color-text-muted)', wordBreak: 'break-all', marginBottom: '0.75rem' }}>
                            {accessPass.accessToken}
                        </div>
                        <p style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', marginBottom: '0.75rem' }}>
                            Show this QR at non-LPR gates, or add it to your phone wallet when enabled.
                        </p>
                        {(accessPass.appleWalletAvailable || accessPass.googleWalletAvailable) && (
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginBottom: '0.75rem' }}>
                                {accessPass.appleWalletAvailable && (
                                    <button
                                        type="button"
                                        className="btn btn-secondary btn-full"
                                        onClick={handleAddAppleWallet}
                                    >
                                        Add to Apple Wallet
                                    </button>
                                )}
                                {accessPass.googleWalletAvailable && (
                                    <button
                                        type="button"
                                        className="btn btn-secondary btn-full"
                                        onClick={handleAddGoogleWallet}
                                    >
                                        Add to Google Wallet
                                    </button>
                                )}
                            </div>
                        )}
                        {accessPass.walletStatusMessage && (
                            <p style={{ fontSize: '0.7rem', color: 'var(--color-text-secondary)', marginBottom: '0.75rem' }}>
                                {accessPass.walletStatusMessage}
                            </p>
                        )}
                        <button type="button" className="btn btn-primary btn-full" onClick={() => setAccessPassOpen(false)}>
                            Close
                        </button>
                    </div>
                </div>
            )}

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
                                                color: star <= reviewRating ? 'var(--color-warning)' : 'rgba(255,255,255,0.1)',
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
                    <div className="card stripe-modal" style={{ maxWidth: '420px', width: '90%' }}>
                        <h2 className="card-title mb-2">Extend Booking</h2>
                        <p style={{ fontSize: '0.9rem', color: 'var(--color-text-secondary)', marginBottom: '1rem' }}>
                            Current session ends at:{' '}
                            <strong>
                                {formatBookingRangeValue(extendingBooking.endDateTime, extendingBooking.pricingType)}
                            </strong>
                        </p>

                        {/* Show other booked slots so user can pick a non-conflicting time */}
                        <BookedSlots reservations={parkingReservations} compact totalSpots={extensionTotalSpots} />

                        <form onSubmit={handleExtensionSubmit}>
                            <div className="form-group">
                                <label className="form-label">Pricing Type</label>
                                <select
                                    className="form-select"
                                    value={extensionPricingType}
                                    onChange={(e) => handleExtensionPricingTypeChange(e.target.value)}
                                >
                                    {PRICING_TYPES.map((opt) => (
                                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                                    ))}
                                </select>
                                <small style={{ display: 'block', marginTop: '0.35rem', color: 'var(--color-text-muted)', fontSize: '0.78rem' }}>
                                    {isDayBasedPricing(extensionPricingType)
                                        ? 'Daily / weekly / monthly bill by full calendar days (clock times ignored).'
                                        : 'Hourly bills by clock hours for the extended period.'}
                                </small>
                            </div>

                            <div className="form-group">
                                <label className="form-label">
                                    {isDayBasedPricing(extensionPricingType) ? 'New End Date' : 'New End Time'}
                                </label>
                                <input
                                    type={isDayBasedPricing(extensionPricingType) ? 'date' : 'datetime-local'}
                                    className="form-input"
                                    value={
                                        isDayBasedPricing(extensionPricingType)
                                            ? toDateOnly(newEndDateTime)
                                            : newEndDateTime
                                    }
                                    min={
                                        isDayBasedPricing(extensionPricingType)
                                            ? toDateOnly(new Date(extendingBooking.endDateTime))
                                            : formatDateTimeLocalInput(new Date(new Date(extendingBooking.endDateTime).getTime() + 60 * 1000))
                                    }
                                    onChange={(e) => {
                                        const value = e.target.value;
                                        setNewEndDateTime(value);
                                        if (!isValidExtensionDate(extendingBooking, value, extensionPricingType)) {
                                            setExtensionValidationError(
                                                isDayBasedPricing(extensionPricingType)
                                                    ? 'New end date must be after the current booking end.'
                                                    : 'New end time must be greater than current booking end time.'
                                            );
                                            setExtensionPrice(null);
                                            return;
                                        }
                                        setExtensionValidationError('');
                                        calculateExtensionPrice(extendingBooking, value, extensionPricingType);
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
                                        <span>Pricing:</span>
                                        <strong>
                                            {PRICING_TYPES.find((p) => p.value === Number(extensionPricingType))?.label
                                                || 'Hourly'}
                                        </strong>
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

