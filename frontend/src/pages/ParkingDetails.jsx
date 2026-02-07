import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import api from '../services/api';

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];
const VEHICLE_TYPES = ['Car', 'Motorcycle', 'SUV', 'Truck', 'Van', 'Electric'];
const PRICING_TYPES = ['Hourly', 'Daily', 'Weekly', 'Monthly'];
const PAYMENT_METHODS = ['Credit Card', 'Debit Card', 'UPI', 'Net Banking', 'Wallet', 'Cash'];
const API_BASE = 'http://localhost:5129';

export default function ParkingDetails() {
    const { id } = useParams();
    const navigate = useNavigate();
    const { isAuthenticated, user } = useAuth();

    const [parking, setParking] = useState(null);
    const [reviews, setReviews] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    const [booking, setBooking] = useState({
        startDateTime: '',
        endDateTime: '',
        pricingType: 0,
        vehicleType: 0,
        vehicleNumber: '',
        vehicleModel: '',
        discountCode: '',
    });

    const [priceBreakdown, setPriceBreakdown] = useState(null);
    const [bookingLoading, setBookingLoading] = useState(false);
    const [bookingSuccess, setBookingSuccess] = useState(null);
    const [showPayment, setShowPayment] = useState(false);
    const [pendingBooking, setPendingBooking] = useState(null);
    const [paymentMethod, setPaymentMethod] = useState(0);

    useEffect(() => {
        fetchParkingDetails();
    }, [id]);

    useEffect(() => {
        if (booking.startDateTime && booking.endDateTime && parking) {
            calculatePrice();
        }
    }, [booking.startDateTime, booking.endDateTime, booking.pricingType, booking.discountCode]);

    const fetchParkingDetails = async () => {
        try {
            const response = await api.getParkingById(id);
            if (response.success && response.data) {
                setParking(response.data);
            } else {
                setError('Parking space not found');
            }

            const reviewsRes = await api.getReviews(id);
            if (reviewsRes.success && reviewsRes.data) {
                setReviews(reviewsRes.data);
            }
        } catch (err) {
            setError('Failed to load parking details');
        }
        setLoading(false);
    };

    const calculatePrice = async () => {
        if (!booking.startDateTime || !booking.endDateTime) return;

        try {
            const response = await api.calculatePrice({
                parkingSpaceId: id,
                startDateTime: new Date(booking.startDateTime).toISOString(),
                endDateTime: new Date(booking.endDateTime).toISOString(),
                pricingType: booking.pricingType,
                discountCode: booking.discountCode || null,
            });

            if (response.success && response.data) {
                setPriceBreakdown(response.data);
            }
        } catch (err) {
            console.error('Price calculation error:', err);
        }
    };

    const handleBooking = async (e) => {
        e.preventDefault();

        if (!isAuthenticated) {
            navigate('/login');
            return;
        }

        setBookingLoading(true);
        setError('');

        try {
            const response = await api.createBooking({
                parkingSpaceId: id,
                startDateTime: new Date(booking.startDateTime).toISOString(),
                endDateTime: new Date(booking.endDateTime).toISOString(),
                pricingType: booking.pricingType,
                vehicleType: booking.vehicleType,
                vehicleNumber: booking.vehicleNumber || null,
                vehicleModel: booking.vehicleModel || null,
                discountCode: booking.discountCode || null,
            });

            if (response.success && response.data) {
                setPendingBooking(response.data);
                // Show pending approval message (status = 0 means Pending)
                setBookingSuccess({
                    reference: response.data.bookingReference,
                    message: 'Booking request submitted! Waiting for owner approval.',
                    isPending: true,
                });
            } else {
                setError(response.message || 'Booking failed');
            }
        } catch (err) {
            setError(err.message || 'Booking failed');
        }

        setBookingLoading(false);
    };

    const handlePayment = async () => {
        setBookingLoading(true);
        setError('');

        try {
            const response = await api.processPayment({
                bookingId: pendingBooking.id,
                paymentMethod: paymentMethod,
            });

            if (response.success && response.data?.success) {
                setBookingSuccess({
                    reference: pendingBooking.bookingReference,
                    message: 'Payment successful! Your booking is confirmed.',
                });
                setShowPayment(false);
            } else {
                setError(response.data?.message || 'Payment failed');
            }
        } catch (err) {
            setError(err.message || 'Payment failed');
        }

        setBookingLoading(false);
    };

    if (loading) {
        return (
            <div className="page">
                <div className="container loading">
                    <div className="spinner"></div>
                </div>
            </div>
        );
    }

    if (error && !parking) {
        return (
            <div className="page">
                <div className="container">
                    <div className="alert alert-error">{error}</div>
                </div>
            </div>
        );
    }

    return (
        <div className="page">
            <div className="container">
                {bookingSuccess && (
                    <div className={`alert ${bookingSuccess.isPending ? 'alert-warning' : 'alert-success'} mb-2`}
                        style={bookingSuccess.isPending ? { background: 'rgba(245, 158, 11, 0.15)', borderColor: '#f59e0b' } : {}}>
                        <strong>{bookingSuccess.message}</strong><br />
                        Booking Reference: <strong>{bookingSuccess.reference}</strong>
                        {bookingSuccess.isPending && (
                            <p style={{ marginTop: '0.5rem', fontSize: '0.9rem' }}>
                                The parking owner will review your request. Once approved, you can proceed with payment from your bookings page.
                            </p>
                        )}
                    </div>
                )}

                <div className="grid" style={{ gridTemplateColumns: '1fr 400px', gap: '2rem' }}>
                    {/* Parking Details */}
                    <div>
                        {/* Image Gallery */}
                        {parking.imageUrls && parking.imageUrls.length > 0 ? (
                            <div style={{ marginBottom: '1.5rem' }}>
                                <div style={{
                                    display: 'grid',
                                    gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
                                    gap: '0.5rem'
                                }}>
                                    {parking.imageUrls.map((url, i) => (
                                        <img
                                            key={i}
                                            src={`${API_BASE}${url}`}
                                            alt={`${parking.title} - ${i + 1}`}
                                            style={{
                                                width: '100%',
                                                height: '150px',
                                                objectFit: 'cover',
                                                borderRadius: 'var(--radius-md)',
                                                cursor: 'pointer'
                                            }}
                                            loading="lazy"
                                        />
                                    ))}
                                </div>
                            </div>
                        ) : (
                            <div className="parking-image" style={{ height: '300px', marginBottom: '1.5rem', fontSize: '5rem' }}>
                                🅿️
                            </div>
                        )}

                        <h1 style={{ marginBottom: '0.5rem' }}>{parking.title}</h1>
                        <div className="parking-location" style={{ fontSize: '1.1rem' }}>
                            📍 {parking.address}, {parking.city}, {parking.state}
                        </div>

                        <div className="flex gap-2 mt-2">
                            <span className="parking-tag">{PARKING_TYPES[parking.parkingType]}</span>
                            <span className="parking-tag">{parking.totalSpots} Total Spots</span>
                            <span className="parking-tag">{parking.availableSpots} Available</span>
                            {parking.is24Hours && <span className="parking-tag">24/7</span>}
                        </div>

                        <div className="card mt-3">
                            <h3 className="card-title">Description</h3>
                            <p>{parking.description}</p>
                        </div>

                        <div className="card mt-2">
                            <h3 className="card-title">Pricing</h3>
                            <div className="grid grid-4" style={{ marginTop: '1rem' }}>
                                <div>
                                    <div className="stat-value" style={{ fontSize: '1.5rem' }}>₹{parking.hourlyRate}</div>
                                    <div className="stat-label">Per Hour</div>
                                </div>
                                <div>
                                    <div className="stat-value" style={{ fontSize: '1.5rem' }}>₹{parking.dailyRate}</div>
                                    <div className="stat-label">Per Day</div>
                                </div>
                                <div>
                                    <div className="stat-value" style={{ fontSize: '1.5rem' }}>₹{parking.weeklyRate}</div>
                                    <div className="stat-label">Per Week</div>
                                </div>
                                <div>
                                    <div className="stat-value" style={{ fontSize: '1.5rem' }}>₹{parking.monthlyRate}</div>
                                    <div className="stat-label">Per Month</div>
                                </div>
                            </div>
                        </div>

                        {parking.amenities?.length > 0 && (
                            <div className="card mt-2">
                                <h3 className="card-title">Amenities</h3>
                                <div className="flex gap-1 mt-1" style={{ flexWrap: 'wrap' }}>
                                    {parking.amenities.map(a => (
                                        <span key={a} className="parking-tag">{a}</span>
                                    ))}
                                </div>
                            </div>
                        )}

                        {parking.specialInstructions && (
                            <div className="card mt-2">
                                <h3 className="card-title">Special Instructions</h3>
                                <p>{parking.specialInstructions}</p>
                            </div>
                        )}

                        {/* Reviews */}
                        <div className="card mt-2">
                            <h3 className="card-title">
                                Reviews ({parking.totalReviews})
                                <span className="rating" style={{ marginLeft: '1rem' }}>
                                    ⭐ {parking.averageRating?.toFixed(1) || 'No ratings'}
                                </span>
                            </h3>

                            {reviews.length === 0 ? (
                                <p className="card-subtitle mt-1">No reviews yet</p>
                            ) : (
                                reviews.map(review => (
                                    <div key={review.id} style={{ borderTop: '1px solid var(--color-border)', paddingTop: '1rem', marginTop: '1rem' }}>
                                        <div className="flex-between">
                                            <strong>{review.userName}</strong>
                                            <span className="rating">⭐ {review.rating}</span>
                                        </div>
                                        {review.title && <p style={{ fontWeight: 500, marginTop: '0.5rem' }}>{review.title}</p>}
                                        {review.comment && <p className="card-subtitle">{review.comment}</p>}
                                        {review.ownerResponse && (
                                            <div style={{ background: 'var(--color-bg-glass)', padding: '0.75rem', borderRadius: 'var(--radius-sm)', marginTop: '0.5rem' }}>
                                                <small>Owner Response:</small>
                                                <p>{review.ownerResponse}</p>
                                            </div>
                                        )}
                                    </div>
                                ))
                            )}
                        </div>
                    </div>

                    {/* Booking Sidebar */}
                    <div>
                        {showPayment ? (
                            <div className="booking-summary">
                                <h3 style={{ marginBottom: '1rem' }}>Complete Payment</h3>

                                <div className="price-row">
                                    <span>Booking Reference</span>
                                    <strong>{pendingBooking.bookingReference}</strong>
                                </div>
                                <div className="price-row total">
                                    <span>Total Amount</span>
                                    <span>₹{pendingBooking.totalAmount}</span>
                                </div>

                                <div className="form-group mt-2">
                                    <label className="form-label">Payment Method</label>
                                    <select
                                        className="form-select"
                                        value={paymentMethod}
                                        onChange={(e) => setPaymentMethod(parseInt(e.target.value))}
                                    >
                                        {PAYMENT_METHODS.map((method, i) => (
                                            <option key={i} value={i}>{method}</option>
                                        ))}
                                    </select>
                                </div>

                                {error && <div className="alert alert-error">{error}</div>}

                                <button
                                    className="btn btn-primary btn-full mt-2"
                                    onClick={handlePayment}
                                    disabled={bookingLoading}
                                >
                                    {bookingLoading ? 'Processing...' : `Pay ₹${pendingBooking.totalAmount}`}
                                </button>

                                <button
                                    className="btn btn-secondary btn-full mt-1"
                                    onClick={() => setShowPayment(false)}
                                >
                                    Cancel
                                </button>
                            </div>
                        ) : (
                            <div className="booking-summary">
                                <h3 style={{ marginBottom: '1rem' }}>Book This Space</h3>

                                <form onSubmit={handleBooking}>
                                    <div className="form-group">
                                        <label className="form-label">Start Date & Time</label>
                                        <input
                                            type="datetime-local"
                                            className="form-input"
                                            value={booking.startDateTime}
                                            onChange={(e) => setBooking(prev => ({ ...prev, startDateTime: e.target.value }))}
                                            required
                                        />
                                    </div>

                                    <div className="form-group">
                                        <label className="form-label">End Date & Time</label>
                                        <input
                                            type="datetime-local"
                                            className="form-input"
                                            value={booking.endDateTime}
                                            onChange={(e) => setBooking(prev => ({ ...prev, endDateTime: e.target.value }))}
                                            required
                                        />
                                    </div>

                                    <div className="form-group">
                                        <label className="form-label">Pricing Type</label>
                                        <select
                                            className="form-select"
                                            value={booking.pricingType}
                                            onChange={(e) => setBooking(prev => ({ ...prev, pricingType: parseInt(e.target.value) }))}
                                        >
                                            {PRICING_TYPES.map((type, i) => (
                                                <option key={i} value={i}>{type}</option>
                                            ))}
                                        </select>
                                    </div>

                                    <div className="form-group">
                                        <label className="form-label">Vehicle Type</label>
                                        <select
                                            className="form-select"
                                            value={booking.vehicleType}
                                            onChange={(e) => setBooking(prev => ({ ...prev, vehicleType: parseInt(e.target.value) }))}
                                        >
                                            {VEHICLE_TYPES.map((type, i) => (
                                                <option key={i} value={i}>{type}</option>
                                            ))}
                                        </select>
                                    </div>

                                    <div className="form-group">
                                        <label className="form-label">Vehicle Number (Optional)</label>
                                        <input
                                            type="text"
                                            className="form-input"
                                            placeholder="e.g., MH12AB1234"
                                            value={booking.vehicleNumber}
                                            onChange={(e) => setBooking(prev => ({ ...prev, vehicleNumber: e.target.value }))}
                                        />
                                    </div>

                                    <div className="form-group">
                                        <label className="form-label">Discount Code</label>
                                        <input
                                            type="text"
                                            className="form-input"
                                            placeholder="Enter code"
                                            value={booking.discountCode}
                                            onChange={(e) => setBooking(prev => ({ ...prev, discountCode: e.target.value }))}
                                        />
                                    </div>

                                    {priceBreakdown && (
                                        <div style={{ borderTop: '1px solid var(--color-border)', paddingTop: '1rem', marginTop: '1rem' }}>
                                            <div className="price-row">
                                                <span>Base ({priceBreakdown.duration} {priceBreakdown.durationUnit})</span>
                                                <span>₹{priceBreakdown.baseAmount}</span>
                                            </div>
                                            <div className="price-row">
                                                <span>Tax (18%)</span>
                                                <span>₹{priceBreakdown.taxAmount}</span>
                                            </div>
                                            <div className="price-row">
                                                <span>Service Fee</span>
                                                <span>₹{priceBreakdown.serviceFee}</span>
                                            </div>
                                            {priceBreakdown.discountAmount > 0 && (
                                                <div className="price-row" style={{ color: 'var(--color-success)' }}>
                                                    <span>Discount</span>
                                                    <span>-₹{priceBreakdown.discountAmount}</span>
                                                </div>
                                            )}
                                            <div className="price-row total">
                                                <span>Total</span>
                                                <span>₹{priceBreakdown.totalAmount}</span>
                                            </div>
                                        </div>
                                    )}

                                    {error && <div className="alert alert-error">{error}</div>}

                                    <button
                                        type="submit"
                                        className="btn btn-primary btn-full mt-2"
                                        disabled={bookingLoading || !priceBreakdown}
                                    >
                                        {bookingLoading ? 'Submitting Request...' : 'Request Booking'}
                                    </button>
                                </form>
                            </div>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
}
