import { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import api from '../services/api';
import LocationMap from '../components/LocationMap';
import BookedSlots from '../components/BookedSlots';
import ImageGallery from '../components/ImageGallery';
import ParkingSlotModal from '../components/ParkingSlotModal';
import { getErrorMessage, handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';
import { useCompany } from '../contexts/CompanyContext';
import corporateService from '../services/corporateService';

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];
import { API_BASE_URL } from '../config';

const VEHICLE_TYPES = ['Car', 'Motorcycle', 'SUV', 'Truck', 'Van', 'Electric'];
const PRICING_TYPES = ['Hourly', 'Daily', 'Weekly', 'Monthly'];
const PAYMENT_METHODS = ['Credit Card', 'Debit Card', 'UPI', 'Net Banking', 'Wallet', 'Cash'];
const API_BASE = API_BASE_URL;

export default function ParkingDetails() {
    const { id } = useParams();
    const navigate = useNavigate();
    const { isAuthenticated, user } = useAuth();
    const { activeCompanyId, isCorporateMode } = useCompany();

    const [parking, setParking] = useState(null);
    const [reviews, setReviews] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(''); // Keep for initial page load errors
    const [isFavorite, setIsFavorite] = useState(false);
    const [favoritesLoading, setFavoritesLoading] = useState(false);

    const [booking, setBooking] = useState({
        startDateTime: '',
        endDateTime: '',
        pricingType: 0,
        vehicleType: 0,
        slotNumber: '',
        vehicleNumber: '',
        vehicleModel: '',
        vehicleColor: '',
        discountCode: '',
    });

    const [priceBreakdown, setPriceBreakdown] = useState(null);
    const [bookingLoading, setBookingLoading] = useState(false);
    const [bookingSuccess, setBookingSuccess] = useState(null);
    const [showPayment, setShowPayment] = useState(false);
    const [pendingBooking, setPendingBooking] = useState(null);
    
    // Corporate state
    const [corporateAllocation, setCorporateAllocation] = useState(null);
    const [isVisitor, setIsVisitor] = useState(false);
    const [visitorName, setVisitorName] = useState('');
    const [visitorPlate, setVisitorPlate] = useState('');
    const [showAllocationRequest, setShowAllocationRequest] = useState(false);
    const [allocationRequesting, setAllocationRequesting] = useState(false);
    const [allocationRequest, setAllocationRequest] = useState({
        totalSlots: 1,
        fixedSlots: 0,
        sharedSlots: 1,
        monthlyRate: 0,
        startDate: '',
        endDate: '',
        maxBookingsPerEmployeePerDay: 1,
        maxBookingsPerEmployeePerWeek: 5,
        priorityThreshold: 1,
        allowedStartTime: '07:00',
        allowedEndTime: '22:00',
        allowWeekends: false,
        leaseReference: '',
    });
    const [paymentMethod, setPaymentMethod] = useState(0);
    const [savedVehicles, setSavedVehicles] = useState([]);
    const [selectedVehicleId, setSelectedVehicleId] = useState('');
    const [showSlotModal, setShowSlotModal] = useState(false);

    useEffect(() => {
        fetchParkingDetails();
        if (isAuthenticated) {
            checkFavoriteStatus();
            fetchUserVehicles();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [id, isAuthenticated]);

    useEffect(() => {
        if (isCorporateMode && activeCompanyId) {
            checkCorporateAllocation();
        } else {
            setCorporateAllocation(null);
        }
    }, [isCorporateMode, activeCompanyId, id]);

    const checkCorporateAllocation = async () => {
        try {
            const res = await corporateService.getAllocations();
            if (res.success && res.data) {
                const activeAlloc = res.data.find(a => a.parkingSpaceId === id && a.status === 1);
                setCorporateAllocation(activeAlloc || null);
            }
        } catch (err) {
            console.error("Failed to fetch corporate allocations", err);
        }
    };

    const handleAllocationRequestChange = (field, value) => {
        setAllocationRequest(prev => {
            const next = { ...prev, [field]: value };
            if (field === 'totalSlots') {
                const total = Math.max(1, parseInt(value, 10) || 1);
                next.totalSlots = total;
                next.fixedSlots = Math.min(parseInt(next.fixedSlots, 10) || 0, total);
                next.sharedSlots = Math.min(parseInt(next.sharedSlots, 10) || 0, total - next.fixedSlots);
            }
            if (field === 'fixedSlots' || field === 'sharedSlots') {
                next[field] = Math.max(0, parseInt(value, 10) || 0);
            }
            return next;
        });
    };

    const handleRequestAllocation = async (e) => {
        e.preventDefault();

        const totalSlots = parseInt(allocationRequest.totalSlots, 10);
        const fixedSlots = parseInt(allocationRequest.fixedSlots, 10);
        const sharedSlots = parseInt(allocationRequest.sharedSlots, 10);

        if (fixedSlots + sharedSlots > totalSlots) {
            showToast.error('Fixed and shared slots cannot exceed total slots.');
            return;
        }

        if (allocationRequest.allowedEndTime <= allocationRequest.allowedStartTime) {
            showToast.error('Allowed end time must be after allowed start time.');
            return;
        }

        setAllocationRequesting(true);
        try {
            const response = await corporateService.requestAllocation({
                parkingSpaceId: id,
                totalSlots,
                fixedSlots,
                sharedSlots,
                monthlyRate: parseFloat(allocationRequest.monthlyRate) || 0,
                startDate: new Date(allocationRequest.startDate).toISOString(),
                endDate: new Date(allocationRequest.endDate).toISOString(),
                leaseReference: allocationRequest.leaseReference || null,
                policy: {
                    maxBookingsPerEmployeePerDay: parseInt(allocationRequest.maxBookingsPerEmployeePerDay, 10),
                    maxBookingsPerEmployeePerWeek: parseInt(allocationRequest.maxBookingsPerEmployeePerWeek, 10),
                    priorityThreshold: parseInt(allocationRequest.priorityThreshold, 10),
                    allowedStartTime: `${allocationRequest.allowedStartTime}:00`,
                    allowedEndTime: `${allocationRequest.allowedEndTime}:00`,
                    allowWeekends: allocationRequest.allowWeekends,
                },
            });

            if (response.success) {
                showToast.success('Allocation request submitted for owner approval.');
                setShowAllocationRequest(false);
                checkCorporateAllocation();
            } else {
                showToast.error(response.message || 'Failed to request allocation.');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to request allocation.'));
        } finally {
            setAllocationRequesting(false);
        }
    };

    const fetchUserVehicles = async () => {
        try {
            const data = await api.getMyVehicles();
            if (data && data.length > 0) {
                setSavedVehicles(data);
                const defaultVeh = data.find(v => v.isDefault) || data[0];
                handleSelectSavedVehicle(defaultVeh.id, data);
            }
        } catch (err) {
            console.error('Failed to load saved vehicles', err);
        }
    };

    const handleSelectSavedVehicle = (vehicleId, vehiclesList = savedVehicles) => {
        setSelectedVehicleId(vehicleId);
        if (!vehicleId) {
            setBooking(prev => ({
                ...prev,
                vehicleType: 0,
                vehicleNumber: '',
                vehicleModel: '',
                vehicleColor: ''
            }));
            return;
        }

        const vehicle = vehiclesList.find(v => v.id === vehicleId);
        if (vehicle) {
            setBooking(prev => ({
                ...prev,
                vehicleType: vehicle.type,
                vehicleNumber: vehicle.licensePlate,
                vehicleModel: `${vehicle.make} ${vehicle.model}`.trim(),
                vehicleColor: vehicle.color || ''
            }));
        }
    };

    const checkFavoriteStatus = async () => {
        try {
            const res = await api.getMyFavorites();
            if (res.success && res.data) {
                const favIds = res.data.map(f => f.id);
                setIsFavorite(favIds.includes(id));
            }
        } catch (err) {
            console.error('Error checking favorite status:', err);
        }
    };

    const toggleFavorite = async () => {
        if (!isAuthenticated) {
            showToast.error("Please log in to save favorites");
            navigate('/login');
            return;
        }

        if (favoritesLoading) return;
        setFavoritesLoading(true);

        try {
            const res = await api.toggleFavorite(id);
            if (res.success) {
                setIsFavorite(res.data);
                if (res.data) showToast.success("Added to favorites");
                else showToast.success("Removed from favorites");
            }
        } catch (err) {
            showToast.error("Failed to update favorite status");
        } finally {
            setFavoritesLoading(false);
        }
    };

    useEffect(() => {
        if (booking.startDateTime && booking.endDateTime && parking) {
            calculatePrice();
        }
    }, [booking.startDateTime, booking.endDateTime, booking.pricingType, booking.discountCode]);

    const slotAvailability = useMemo(() => {
        if (!parking || parking.totalSpots <= 1) return [];
        const reservations = parking.activeReservations || [];
        const hasTimeRange = Boolean(booking.startDateTime && booking.endDateTime);
        const selectedStart = hasTimeRange ? new Date(booking.startDateTime) : null;
        const selectedEnd = hasTimeRange ? new Date(booking.endDateTime) : null;

        return Array.from({ length: parking.totalSpots }, (_, i) => {
            const slotNumber = i + 1;
            const slotReservations = reservations.filter(r => r.slotNumber === slotNumber);
            const blockedForSelection = hasTimeRange
                ? slotReservations.some(r => {
                    const reservedStart = new Date(r.startDateTime);
                    const reservedEnd = new Date(r.endDateTime);
                    return selectedStart < reservedEnd && selectedEnd > reservedStart;
                })
                : false;

            return {
                slotNumber,
                blockedForSelection,
                reservations: slotReservations
            };
        });
    }, [parking, booking.startDateTime, booking.endDateTime]);

    // Auto-clear slot selection if it becomes blocked by the chosen time range
    useEffect(() => {
        if (!booking.slotNumber || slotAvailability.length === 0) return;
        const selectedSlotData = slotAvailability.find(
            s => String(s.slotNumber) === String(booking.slotNumber)
        );
        if (selectedSlotData?.blockedForSelection) {
            setBooking(prev => ({ ...prev, slotNumber: '' }));
            showToast.error(`Slot ${booking.slotNumber} is already booked for your selected time. Please choose another slot.`);
        }
    }, [slotAvailability]);

    const fetchParkingDetails = async () => {
        try {
            const response = await api.getParkingById(id);
            if (response.success && response.data) {
                setParking(response.data);
            } else {
                setError('Parking space not found');
            }

            const reviewsRes = await api.getReviewsByParkingSpace(id);
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

        if (parking?.totalSpots > 1 && !booking.slotNumber && !corporateAllocation) {
            showToast.error('Please select a parking slot');
            return;
        }

        if (parking?.totalSpots > 1 && booking.slotNumber && booking.startDateTime && booking.endDateTime && !corporateAllocation) {
            const selectedSlotNumber = parseInt(booking.slotNumber, 10);
            const selectedSlot = slotAvailability.find(s => s.slotNumber === selectedSlotNumber);
            if (selectedSlot?.blockedForSelection) {
                showToast.error(`Slot ${selectedSlotNumber} is already booked for the selected time`);
                return;
            }
        }

        setBookingLoading(true);

        try {
            // Corporate Booking Flow
            if (corporateAllocation) {
                const payload = {
                    allocationId: corporateAllocation.id,
                    startDateTime: new Date(booking.startDateTime).toISOString(),
                    endDateTime: new Date(booking.endDateTime).toISOString(),
                };
                
                let res;
                if (isVisitor) {
                    res = await corporateService.bookVisitorParking({
                        ...payload,
                        visitorName: visitorName,
                        visitorLicensePlate: visitorPlate,
                        accessExpiry: new Date(booking.endDateTime).toISOString()
                    });
                } else {
                    res = await corporateService.bookEmployeeParking({
                        ...payload,
                        vehicleType: booking.vehicleType,
                        vehicleNumber: booking.vehicleNumber || null
                    });
                }
                
                if (res.success) {
                    setBookingSuccess({
                        reference: res.data.booking?.bookingReference || 'WAITLIST',
                        message: res.data.waitlist ? 'Added to waitlist based on allocation policy.' : 'Corporate Booking Confirmed!',
                        isPending: false,
                    });
                    showToast.success('Corporate booking handled successfully');
                    setBooking(prev => ({ ...prev, slotNumber: '' }));
                    fetchParkingDetails();
                } else {
                    showToast.error(res.message || 'Corporate booking failed');
                }
                setBookingLoading(false);
                return;
            }

            // Standard User Booking Flow
            const response = await api.createBooking({
                parkingSpaceId: id,
                startDateTime: new Date(booking.startDateTime).toISOString(),
                endDateTime: new Date(booking.endDateTime).toISOString(),
                pricingType: booking.pricingType,
                vehicleType: booking.vehicleType,
                slotNumber: booking.slotNumber ? parseInt(booking.slotNumber, 10) : null,
                vehicleNumber: booking.vehicleNumber || null,
                vehicleModel: booking.vehicleModel || null,
                vehicleColor: booking.vehicleColor || null,
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
                showToast.success('Booking request submitted! Waiting for owner approval.');

                // Clear slot selection before refresh so the validation effect doesn't false-fire
                setBooking(prev => ({ ...prev, slotNumber: '' }));
                // Re-fetch parking details to update reservation list
                fetchParkingDetails();
            } else {
                showToast.error(getErrorMessage(response));
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Booking failed'));
        }

        setBookingLoading(false);
    };

    const handlePayment = async () => {
        setBookingLoading(true);

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
                showToast.success('Payment successful! Your booking is confirmed.');
            } else {
                showToast.error(getErrorMessage(response.data || response));
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Payment failed'));
        }

        setBookingLoading(false);
    };

    const formatDateTime = (dateStr) => {
        const d = new Date(dateStr);
        return d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) +
            ' ' + d.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' });
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
                        <ImageGallery images={parking.imageUrls} title={parking.title} />

                        <div className="flex-between align-center" style={{ marginBottom: '0.5rem' }}>
                            <h1 style={{ margin: 0 }}>{parking.title}</h1>
                            <button
                                className="btn btn-outline"
                                onClick={toggleFavorite}
                                disabled={favoritesLoading}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.5rem',
                                    fontSize: '1rem',
                                    padding: '0.5rem 1rem',
                                    borderRadius: 'var(--radius-full)',
                                    borderColor: isFavorite ? 'var(--color-primary)' : 'var(--color-border)',
                                    color: isFavorite ? 'var(--color-primary)' : 'inherit'
                                }}
                            >
                                <span style={{ fontSize: '1.2rem' }}>{isFavorite ? '❤️' : '🤍'}</span>
                                {isFavorite ? 'Saved' : 'Save'}
                            </button>
                        </div>
                        <div className="parking-location" style={{ fontSize: '1.1rem' }}>
                            📍 {parking.address}, {parking.city}, {parking.state}
                        </div>
                        <div style={{ marginTop: '0.75rem' }}>
                            <a
                                href={`https://www.google.com/maps/dir/?api=1&destination=${parking.latitude},${parking.longitude}`}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="btn-navigate"
                                style={{ padding: '0.4rem 0.8rem', fontSize: '0.85rem' }}
                            >
                                🗺️ Get Directions
                            </a>
                        </div>

                        <div className="flex gap-2 mt-2">
                            <span className="parking-tag">{PARKING_TYPES[parking.parkingType]}</span>
                            <span className="parking-tag">{parking.totalSpots} Total Spots</span>
                            <span className="parking-tag">{parking.availableSpots} Available</span>
                            {parking.is24Hours && <span className="parking-tag">24/7</span>}
                        </div>

                        {/* Chat with Owner */}
                        {isAuthenticated && user?.id !== parking.ownerId && (
                            <button
                                className="btn btn-secondary mt-2"
                                onClick={() => navigate(`/chat?parkingSpaceId=${parking.id}`)}
                                style={{
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    gap: '0.5rem'
                                }}
                            >
                                💬 Chat with Owner
                            </button>
                        )}

                        <div className="card mt-3">
                            <h3 className="card-title">Description</h3>
                            <p>{parking.description}</p>
                        </div>

                        {/* Current Reservations Section */}
                        <BookedSlots reservations={parking.activeReservations} totalSpots={parking.totalSpots} />

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

                        {/* Location Map */}
                        <div className="card mt-2">
                            <h3 className="card-title">Location</h3>
                            <div style={{ marginTop: '0.75rem' }}>
                                <LocationMap
                                    singleLocation={{
                                        latitude: parking.latitude,
                                        longitude: parking.longitude,
                                        title: parking.title
                                    }}
                                    height="250px"
                                />
                            </div>
                        </div>

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
                                {corporateAllocation ? (
                                    <div style={{ background: 'rgba(16, 185, 129, 0.1)', border: '1px solid #10b981', padding: '15px', borderRadius: '8px', marginBottom: '1rem', color: '#10b981' }}>
                                        <strong>🏢 Corporate Booking Available</strong>
                                        <p style={{ margin: '5px 0 0', fontSize: '0.9rem' }}>
                                            Your company has an active allocation here. Costs are covered by {activeCompanyId ? 'your employer' : 'your company'}!
                                        </p>
                                    </div>
                                ) : (
                                    <>
                                        <h3 style={{ marginBottom: '1rem' }}>Book This Space</h3>
                                        {isCorporateMode && activeCompanyId && (
                                            <div style={{ background: 'rgba(139, 92, 246, 0.1)', border: '1px solid rgba(139, 92, 246, 0.45)', padding: '15px', borderRadius: '8px', marginBottom: '1rem' }}>
                                                <strong style={{ color: '#c4b5fd' }}>No corporate allocation at this parking space</strong>
                                                <p style={{ margin: '5px 0 12px', fontSize: '0.9rem', color: '#cbd5e1' }}>
                                                    Request slots for your company and the parking owner can approve them from vendor bookings.
                                                </p>
                                                <button
                                                    type="button"
                                                    className="btn btn-secondary"
                                                    onClick={() => setShowAllocationRequest(true)}
                                                >
                                                    Request Corporate Allocation
                                                </button>
                                            </div>
                                        )}
                                    </>
                                )}

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

                                    {parking.totalSpots > 1 && !corporateAllocation && (
                                        <div className="form-group">
                                            <label className="form-label">Parking Slot</label>
                                            {/* Selected slot preview */}
                                            {booking.slotNumber && (
                                                <div style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '0.75rem',
                                                    background: 'rgba(99,102,241,0.12)',
                                                    border: '1px solid rgba(99,102,241,0.4)',
                                                    borderRadius: '10px',
                                                    padding: '0.75rem 1rem',
                                                    marginBottom: '0.75rem',
                                                }}>
                                                    <span style={{ fontSize: '1.4rem' }}>🅿️</span>
                                                    <div>
                                                        <div style={{ fontWeight: 700, color: '#818cf8' }}>Slot {booking.slotNumber} Selected</div>
                                                        <div style={{ fontSize: '0.8rem', color: '#a0a0b0' }}>Click below to change</div>
                                                    </div>
                                                </div>
                                            )}
                                            <button
                                                type="button"
                                                onClick={() => setShowSlotModal(true)}
                                                style={{
                                                    width: '100%',
                                                    padding: '0.875rem 1rem',
                                                    background: 'var(--color-bg-tertiary)',
                                                    border: '1px dashed rgba(99,102,241,0.5)',
                                                    borderRadius: 'var(--radius-md)',
                                                    color: booking.slotNumber ? '#818cf8' : 'var(--color-text-muted)',
                                                    fontWeight: 600,
                                                    cursor: 'pointer',
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'center',
                                                    gap: '0.5rem',
                                                    fontSize: '0.95rem',
                                                    transition: 'all 0.2s',
                                                }}
                                            >
                                                🗺️ {booking.slotNumber ? `Change Slot (Currently: P${booking.slotNumber})` : `View Parking Map & Choose Slot (${parking.totalSpots} slots)`}
                                            </button>
                                            <small style={{ display: 'block', marginTop: '0.4rem', color: 'var(--color-text-muted)', fontSize: '0.78rem' }}>
                                                {booking.startDateTime && booking.endDateTime
                                                    ? '✓ Availability shown in real-time for your selected time range.'
                                                    : '⚠ Select start & end time first to see live availability.'}
                                            </small>
                                        </div>
                                    )}

                                    {!corporateAllocation && (
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
                                    )}

                                    {corporateAllocation && (
                                        <div className="form-group" style={{ background: 'var(--color-bg-tertiary)', padding: '10px', borderRadius: '8px', marginBottom: '15px' }}>
                                            <label style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer', margin: 0 }}>
                                                <input 
                                                    type="checkbox" 
                                                    checked={isVisitor}
                                                    onChange={e => setIsVisitor(e.target.checked)}
                                                    style={{ width: '18px', height: '18px', accentColor: '#10b981' }}
                                                />
                                                <strong style={{ color: 'white' }}>This is for a Visitor</strong>
                                            </label>
                                        </div>
                                    )}

                                    {isVisitor ? (
                                        <>
                                            <div className="form-group">
                                                <label className="form-label">Visitor Name</label>
                                                <input
                                                    type="text"
                                                    className="form-input"
                                                    placeholder="Required"
                                                    value={visitorName}
                                                    onChange={(e) => setVisitorName(e.target.value)}
                                                    required
                                                />
                                            </div>
                                            <div className="form-group">
                                                <label className="form-label">Visitor License Plate</label>
                                                <input
                                                    type="text"
                                                    className="form-input"
                                                    placeholder="Required"
                                                    value={visitorPlate}
                                                    onChange={(e) => setVisitorPlate(e.target.value)}
                                                    required
                                                />
                                            </div>
                                        </>
                                    ) : (
                                        <>
                                            <div className="form-group">
                                                <label className="form-label">Saved Vehicles</label>
                                                <select
                                                    className="form-select"
                                                    value={selectedVehicleId}
                                                    onChange={(e) => handleSelectSavedVehicle(e.target.value)}
                                                >
                                                    <option value="">-- Enter Details Manually --</option>
                                                    {savedVehicles.map(v => (
                                                        <option key={v.id} value={v.id}>
                                                            {v.make} {v.model} ({v.licensePlate})
                                                        </option>
                                                    ))}
                                                </select>
                                            </div>

                                            <div className="form-group">
                                                <label className="form-label">Vehicle Type</label>
                                                <select
                                                    className="form-select"
                                                    value={booking.vehicleType}
                                                    onChange={(e) => {
                                                        setBooking(prev => ({ ...prev, vehicleType: parseInt(e.target.value) }));
                                                        setSelectedVehicleId('');
                                                    }}
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
                                                    onChange={(e) => {
                                                        setBooking(prev => ({ ...prev, vehicleNumber: e.target.value }));
                                                        setSelectedVehicleId('');
                                                    }}
                                                />
                                            </div>
                                            
                                            {!corporateAllocation && (
                                                <div className="form-group">
                                                    <label className="form-label">Vehicle Color (Optional)</label>
                                                    <input
                                                        type="text"
                                                        className="form-input"
                                                        placeholder="e.g., Red, Blue"
                                                        value={booking.vehicleColor}
                                                        onChange={(e) => {
                                                            setBooking(prev => ({ ...prev, vehicleColor: e.target.value }));
                                                            setSelectedVehicleId('');
                                                        }}
                                                    />
                                                </div>
                                            )}
                                        </>
                                    )}

                                    {!corporateAllocation && (
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
                                    )}

                                    {priceBreakdown && !corporateAllocation && (
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
                                        disabled={bookingLoading || (!priceBreakdown && !corporateAllocation) || (corporateAllocation && (!booking.startDateTime || !booking.endDateTime))}
                                        style={corporateAllocation ? { background: '#10b981', borderColor: '#10b981' } : {}}
                                    >
                                        {bookingLoading ? 'Submitting Request...' : corporateAllocation ? 'Confirm Corporate Booking' : 'Request Booking'}
                                    </button>
                                </form>
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {showAllocationRequest && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.72)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem' }}>
                    <div className="card" style={{ width: '100%', maxWidth: '620px', maxHeight: '90vh', overflowY: 'auto' }}>
                        <h3 className="card-title">Request Corporate Allocation</h3>
                        <p className="card-subtitle mb-2">{parking?.title}</p>
                        <form onSubmit={handleRequestAllocation}>
                            <div className="grid grid-3" style={{ gap: '1rem' }}>
                                <div className="form-group">
                                    <label className="form-label">Total Slots</label>
                                    <input type="number" min="1" max={parking?.totalSpots || 1000} className="form-input" value={allocationRequest.totalSlots} onChange={(e) => handleAllocationRequestChange('totalSlots', e.target.value)} required />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Fixed Slots</label>
                                    <input type="number" min="0" className="form-input" value={allocationRequest.fixedSlots} onChange={(e) => handleAllocationRequestChange('fixedSlots', e.target.value)} required />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Shared Slots</label>
                                    <input type="number" min="0" className="form-input" value={allocationRequest.sharedSlots} onChange={(e) => handleAllocationRequestChange('sharedSlots', e.target.value)} required />
                                </div>
                            </div>

                            <div className="grid grid-3" style={{ gap: '1rem' }}>
                                <div className="form-group">
                                    <label className="form-label">Monthly Rate</label>
                                    <input type="number" min="0" step="0.01" className="form-input" value={allocationRequest.monthlyRate} onChange={(e) => handleAllocationRequestChange('monthlyRate', e.target.value)} required />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Start Date</label>
                                    <input type="date" className="form-input" value={allocationRequest.startDate} onChange={(e) => handleAllocationRequestChange('startDate', e.target.value)} required />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">End Date</label>
                                    <input type="date" className="form-input" value={allocationRequest.endDate} onChange={(e) => handleAllocationRequestChange('endDate', e.target.value)} required />
                                </div>
                            </div>

                            <div className="form-group">
                                <label className="form-label">Lease Reference</label>
                                <input
                                    type="text"
                                    maxLength="100"
                                    className="form-input"
                                    placeholder="Optional contract or purchase order reference"
                                    value={allocationRequest.leaseReference}
                                    onChange={(e) => handleAllocationRequestChange('leaseReference', e.target.value)}
                                />
                            </div>

                            <div className="grid grid-3" style={{ gap: '1rem' }}>
                                <div className="form-group">
                                    <label className="form-label">Max/Day</label>
                                    <input type="number" min="1" className="form-input" value={allocationRequest.maxBookingsPerEmployeePerDay} onChange={(e) => handleAllocationRequestChange('maxBookingsPerEmployeePerDay', e.target.value)} required />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Max/Week</label>
                                    <input type="number" min="1" className="form-input" value={allocationRequest.maxBookingsPerEmployeePerWeek} onChange={(e) => handleAllocationRequestChange('maxBookingsPerEmployeePerWeek', e.target.value)} required />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Min Priority</label>
                                    <input type="number" min="1" max="10" className="form-input" value={allocationRequest.priorityThreshold} onChange={(e) => handleAllocationRequestChange('priorityThreshold', e.target.value)} required />
                                </div>
                            </div>

                            <div className="grid grid-3" style={{ gap: '1rem' }}>
                                <div className="form-group">
                                    <label className="form-label">Allowed Start</label>
                                    <input type="time" className="form-input" value={allocationRequest.allowedStartTime} onChange={(e) => handleAllocationRequestChange('allowedStartTime', e.target.value)} required />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Allowed End</label>
                                    <input type="time" className="form-input" value={allocationRequest.allowedEndTime} onChange={(e) => handleAllocationRequestChange('allowedEndTime', e.target.value)} required />
                                </div>
                                <div className="form-group" style={{ display: 'flex', alignItems: 'center', paddingTop: '1.8rem' }}>
                                    <label style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', cursor: 'pointer' }}>
                                        <input type="checkbox" checked={allocationRequest.allowWeekends} onChange={(e) => handleAllocationRequestChange('allowWeekends', e.target.checked)} />
                                        Allow weekends
                                    </label>
                                </div>
                            </div>

                            <div className="flex gap-1 mt-2" style={{ justifyContent: 'flex-end' }}>
                                <button type="button" className="btn btn-secondary" onClick={() => setShowAllocationRequest(false)}>Cancel</button>
                                <button type="submit" className="btn btn-primary" disabled={allocationRequesting}>
                                    {allocationRequesting ? 'Submitting...' : 'Submit Request'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Slot selection modal */}
            {parking?.totalSpots > 1 && (
                <ParkingSlotModal
                    isOpen={showSlotModal}
                    onClose={() => setShowSlotModal(false)}
                    slotAvailability={slotAvailability}
                    selectedSlot={booking.slotNumber}
                    onSelect={(slotNum) => setBooking(prev => ({ ...prev, slotNumber: slotNum }))}
                    hasTimeRange={Boolean(booking.startDateTime && booking.endDateTime)}
                />
            )}
        </div>
    );
}
