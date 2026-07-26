import { useState, useEffect, useCallback, useRef, Suspense, lazy } from 'react';
import { Link } from 'react-router-dom';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';
import { handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';
import INDIAN_STATES_CITIES, { STATES } from '../utils/indianStatesCities';
// Leaflet map picker — load only with VendorListings form (page is already route-lazy)
const LocationPicker = lazy(() => import('../components/LocationPicker'));

import { API_BASE_URL } from '../config';

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];
const API_BASE = API_BASE_URL;

// Notification types that should trigger a refresh of vendor listings
const REFRESH_TRIGGERS = [
    'booking.created',     // New booking
    'booking.cancelled',   // Booking cancelled
    'booking.checkin',     // User checked in
    'booking.checkout',    // User checked out
    'extension.requested',
    'extension.approved',
    'extension.rejected'
];

export default function VendorListings() {
    const [listings, setListings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showForm, setShowForm] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [uploadingId, setUploadingId] = useState(null);
    const [uploadProgress, setUploadProgress] = useState('');
    const [bookings, setBookings] = useState([]);
    const [ancillaryBySpace, setAncillaryBySpace] = useState({}); // parkingSpaceId -> services[]
    const [ancillaryForm, setAncillaryForm] = useState({}); // parkingSpaceId -> draft form
    const [ancillarySaving, setAncillarySaving] = useState(null);
    const [ancillaryOpenId, setAncillaryOpenId] = useState(null);
    const titleInputRef = useRef(null);

    const { subscribeToRefresh } = useNotificationContext();

    const emptyForm = {
        title: '',
        description: '',
        address: '',
        city: '',
        state: '',
        country: 'India',
        postalCode: '',
        latitude: '',
        longitude: '',
        parkingType: 0,
        listingCategory: 0, // 0 Commercial, 1 Residential
        instantBook: false,
        totalSpots: 10,
        hourlyRate: 50,
        dailyRate: 400,
        weeklyRate: 2500,
        monthlyRate: 8000,
        is24Hours: true,
        amenities: '',
        specialInstructions: '',
        isLprEnabled: false,
        isDynamicPricingEnabled: false,
        dynamicMinMultiplier: 0.8,
        dynamicMaxMultiplier: 1.75,
        peakHourMultiplier: 1.25,
        weekendMultiplier: 1.15,
        timeZoneId: 'Asia/Kolkata',
        hasEvCharging: false,
        evChargerCount: 1,
        evChargingRatePerHour: 25,
        evIdleRatePerHour: 50,
        evIdleGraceMinutes: 15,
        evPricingMode: 0, // 0=Hourly, 1=PerKwh
        evRatePerKwh: 15,
        isBayGuidanceEnabled: false,
        isValetEnabled: false,
        defaultFacilityLevel: '',
        defaultFacilityZone: '',
        indoorGuidanceNotes: '',
    };

    const [form, setForm] = useState(emptyForm);

    const fetchListings = useCallback(async (shouldSetLoading = true) => {
        try {
            const response = await api.getMyListings();
            if (response.success && response.data) {
                setListings(response.data);
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to load listings'));
        }
        if (shouldSetLoading) setLoading(false);
    }, []);

    const fetchAncillary = useCallback(async () => {
        try {
            const response = await api.getMyAncillaryServices();
            if (response.success && Array.isArray(response.data)) {
                const map = {};
                for (const s of response.data) {
                    const key = s.parkingSpaceId;
                    if (!map[key]) map[key] = [];
                    map[key].push(s);
                }
                setAncillaryBySpace(map);
            }
        } catch (err) {
            // non-fatal
        }
    }, []);

    const fetchBookings = useCallback(async () => {
        try {
            const response = await api.getVendorBookings();
            if (response.success && response.data?.bookings) {
                setBookings(response.data.bookings);
            }
        } catch (err) {
            console.error('Fetch bookings error:', err);
        }
    }, []);

    // Initial data load
    useEffect(() => {
        const loadData = async () => {
            setLoading(true);
            await Promise.all([
                fetchListings(false),
                fetchBookings(),
                fetchAncillary(),
            ]);
            setLoading(false);
        };
        loadData();
    }, [fetchListings, fetchBookings, fetchAncillary]);

    // Subscribe to real-time refresh events
    useEffect(() => {
        const unsubscribe = subscribeToRefresh('VendorListings', REFRESH_TRIGGERS, () => {
            // console.log('🔄 VendorListings: Auto-refreshing due to notification');
            fetchListings(false); // background refresh
            fetchBookings();
            fetchAncillary();
        });
        return unsubscribe;
    }, [subscribeToRefresh, fetchListings, fetchBookings, fetchAncillary]);

    const emptyAncillaryDraft = { name: '', description: '', price: '', durationMinutes: '', sortOrder: 0 };

    const handleCreateAncillary = async (parkingSpaceId) => {
        const draft = ancillaryForm[parkingSpaceId] || emptyAncillaryDraft;
        if (!draft.name?.trim()) {
            showToast.error('Add-on name is required');
            return;
        }
        const price = parseFloat(draft.price);
        if (Number.isNaN(price) || price < 0) {
            showToast.error('Enter a valid price');
            return;
        }
        setAncillarySaving(parkingSpaceId);
        try {
            const res = await api.createAncillaryService({
                parkingSpaceId,
                name: draft.name.trim(),
                description: draft.description?.trim() || null,
                price,
                durationMinutes: draft.durationMinutes ? parseInt(draft.durationMinutes, 10) : null,
                sortOrder: parseInt(draft.sortOrder, 10) || 0,
                isActive: true,
            });
            if (res.success) {
                showToast.success('Add-on created');
                setAncillaryForm(prev => ({ ...prev, [parkingSpaceId]: { ...emptyAncillaryDraft } }));
                await fetchAncillary();
            } else {
                showToast.error(res.message || 'Failed to create add-on');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to create add-on'));
        } finally {
            setAncillarySaving(null);
        }
    };

    const handleToggleAncillaryActive = async (service) => {
        try {
            const res = await api.updateAncillaryService(service.id, { isActive: !service.isActive });
            if (res.success) {
                showToast.success(service.isActive ? 'Add-on deactivated' : 'Add-on activated');
                await fetchAncillary();
            } else {
                showToast.error(res.message || 'Update failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Update failed'));
        }
    };

    const handleDeactivateAncillary = async (serviceId) => {
        try {
            const res = await api.deactivateAncillaryService(serviceId);
            if (res.success) {
                showToast.success('Add-on deactivated');
                await fetchAncillary();
            } else {
                showToast.error(res.message || 'Deactivate failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Deactivate failed'));
        }
    };

    const getActiveBookingsForListing = (listingId, listingReservations = []) => {
        const now = new Date();
        // Status values: 0=Pending, 1=Confirmed, 2=InProgress, 6=AwaitingPayment
        const activeStatuses = [0, 1, 2, 6, 8, 9, 'Pending', 'Confirmed', 'InProgress', 'AwaitingPayment', 'Awaitingpayment', 'PendingExtension', 'AwaitingExtensionPayment'];

        // Try to get full booking objects first (contains userName)
        const detailedBookings = bookings.filter(b => {
            const bParkingId = (b.parkingSpaceId || b.ParkingSpaceId || '').toString().toLowerCase();
            const lId = (listingId || '').toString().toLowerCase();
            return bParkingId === lId &&
                activeStatuses.includes(b.status) &&
                new Date(b.endDateTime) > now;
        }).sort((a, b) => new Date(a.startDateTime) - new Date(b.startDateTime));

        if (detailedBookings.length > 0) return detailedBookings;

        // Fallback to basic reservations from listing DTO if detailed bookings not loaded/available
        if (listingReservations && listingReservations.length > 0) {
            return listingReservations.map(r => ({
                id: `res-${r.startDateTime}`,
                userName: r.userName || 'Reserved',
                startDateTime: r.startDateTime,
                endDateTime: r.endDateTime,
                slotNumber: r.slotNumber,
                isBasic: true
            }));
        }

        return [];
    };

    const getStatusLabel = (status) => {
        const labels = { 0: 'Pending', 1: 'Confirmed', 2: 'InProgress', 6: 'AwaitingPayment', 8: 'Extension Pending', 9: 'Extension Payment Due' };
        return labels[status] || status;
    };

    const getStatusStyle = (status) => {
        const isConfirmed = status === 1 || status === 'Confirmed';
        const isInProgress = status === 2 || status === 'InProgress';
        return {
            background: isConfirmed ? 'rgba(16,185,129,0.2)' :
                isInProgress ? 'rgba(234,179,8,0.2)' : 'rgba(107,114,128,0.2)',
            color: isConfirmed ? '#10b981' : isInProgress ? '#eab308' : '#9ca3af'
        };
    };

    const formatDateTime = (dateStr) => {
        const d = new Date(dateStr);
        return d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) +
            ' ' + d.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        const data = {
            ...form,
            latitude: parseFloat(form.latitude) || 0,
            longitude: parseFloat(form.longitude) || 0,
            totalSpots: parseInt(form.totalSpots),
            hourlyRate: parseFloat(form.hourlyRate),
            dailyRate: parseFloat(form.dailyRate),
            weeklyRate: parseFloat(form.weeklyRate),
            monthlyRate: parseFloat(form.monthlyRate),
            parkingType: parseInt(form.parkingType),
            amenities: form.amenities ? form.amenities.split(',').map(a => a.trim()) : [],
            isLprEnabled: !!form.isLprEnabled,
            isDynamicPricingEnabled: !!form.isDynamicPricingEnabled,
            dynamicMinMultiplier: form.isDynamicPricingEnabled ? parseFloat(form.dynamicMinMultiplier) || 0.8 : undefined,
            dynamicMaxMultiplier: form.isDynamicPricingEnabled ? parseFloat(form.dynamicMaxMultiplier) || 1.75 : undefined,
            peakHourMultiplier: form.isDynamicPricingEnabled ? parseFloat(form.peakHourMultiplier) || 1.25 : undefined,
            weekendMultiplier: form.isDynamicPricingEnabled ? parseFloat(form.weekendMultiplier) || 1.15 : undefined,
            timeZoneId: form.timeZoneId || 'UTC',
            hasEvCharging: !!form.hasEvCharging,
            evChargerCount: form.hasEvCharging ? parseInt(form.evChargerCount, 10) || 0 : 0,
            evChargingRatePerHour: form.hasEvCharging ? parseFloat(form.evChargingRatePerHour) || 0 : 0,
            evIdleRatePerHour: form.hasEvCharging ? parseFloat(form.evIdleRatePerHour) || 0 : 0,
            evIdleGraceMinutes: form.hasEvCharging ? parseInt(form.evIdleGraceMinutes, 10) || 15 : 15,
            evPricingMode: form.hasEvCharging ? (parseInt(form.evPricingMode, 10) || 0) : 0,
            evRatePerKwh: form.hasEvCharging ? parseFloat(form.evRatePerKwh) || 0 : 0,
            listingCategory: parseInt(form.listingCategory, 10) || 0,
            instantBook: !!form.instantBook,
            isBayGuidanceEnabled: !!form.isBayGuidanceEnabled,
            isValetEnabled: !!form.isValetEnabled,
            defaultFacilityLevel: form.isBayGuidanceEnabled ? (form.defaultFacilityLevel || null) : null,
            defaultFacilityZone: form.isBayGuidanceEnabled ? (form.defaultFacilityZone || null) : null,
            indoorGuidanceNotes: form.isBayGuidanceEnabled ? (form.indoorGuidanceNotes || null) : null,
        };

        try {
            let response;
            if (editingId) {
                response = await api.updateParking(editingId, data);
            } else {
                response = await api.createParking(data);
            }

            if (response.success) {
                showToast.success(editingId ? 'Listing updated successfully!' : 'Listing created successfully!');
                setShowForm(false);
                setEditingId(null);
                setForm(emptyForm);
                await Promise.all([
                    fetchListings(),
                    fetchBookings(),
                ]);
            } else {
                showToast.error(response.message || 'Operation failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to save listing'));
        }
    };

    const handleEdit = (listing) => {
        setForm({
            ...emptyForm,
            ...listing,
            amenities: listing.amenities?.join(', ') || '',
            isDynamicPricingEnabled: !!listing.isDynamicPricingEnabled,
            dynamicMinMultiplier: listing.dynamicMinMultiplier ?? 0.8,
            dynamicMaxMultiplier: listing.dynamicMaxMultiplier ?? 1.75,
            peakHourMultiplier: listing.peakHourMultiplier ?? 1.25,
            weekendMultiplier: listing.weekendMultiplier ?? 1.15,
            timeZoneId: listing.timeZoneId || 'Asia/Kolkata',
            hasEvCharging: !!listing.hasEvCharging,
            evChargerCount: listing.evChargerCount ?? 1,
            evChargingRatePerHour: listing.evChargingRatePerHour ?? 25,
            evIdleRatePerHour: listing.evIdleRatePerHour ?? 50,
            evIdleGraceMinutes: listing.evIdleGraceMinutes ?? 15,
            evPricingMode: listing.evPricingMode ?? 0,
            evRatePerKwh: listing.evRatePerKwh ?? 15,
            listingCategory: listing.listingCategory ?? 0,
            instantBook: !!listing.instantBook,
            isBayGuidanceEnabled: !!listing.isBayGuidanceEnabled,
            isValetEnabled: !!listing.isValetEnabled,
            defaultFacilityLevel: listing.defaultFacilityLevel || '',
            defaultFacilityZone: listing.defaultFacilityZone || '',
            indoorGuidanceNotes: listing.indoorGuidanceNotes || '',
        });
        setEditingId(listing.id);
        setShowForm(true);
        setTimeout(() => {
            if (titleInputRef.current) {
                titleInputRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' });
                titleInputRef.current.focus();
            }
        }, 100);
    };

    const handleDelete = async (id) => {
        if (!window.confirm('Are you sure you want to delete this listing?')) return;

        try {
            const response = await api.deleteParking(id);
            if (response.success) {
                showToast.success('Listing deleted successfully!');
                await Promise.all([
                    fetchListings(),
                    fetchBookings(),
                ]);
            } else {
                showToast.error(response.message || 'Delete failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to delete listing'));
        }
    };

    const handleFileUpload = async (listingId, files) => {
        if (!files || files.length === 0) return;

        setUploadingId(listingId);
        setUploadProgress('Starting upload...');

        const successUrls = [];
        const errors = [];

        try {
            for (let i = 0; i < files.length; i++) {
                const file = files[i];
                setUploadProgress(`Uploading ${i + 1}/${files.length}...`);

                try {
                    // 1. Get Pre-signed URL
                    const signResponse = await api.getPresignedUrl(listingId, file.name, file.type);
                    if (!signResponse.success) throw new Error('Failed to get upload URL');

                    const { uploadUrl, publicUrl } = signResponse.data;

                    // 2. Upload to R2
                    const uploadResponse = await fetch(uploadUrl, {
                        method: 'PUT',
                        headers: {
                            'Content-Type': file.type
                        },
                        body: file
                    });

                    if (!uploadResponse.ok) throw new Error('Failed to upload to storage');

                    successUrls.push(publicUrl);

                } catch (err) {
                    console.error(`Upload error for ${file.name}:`, err);
                    errors.push(file.name);
                }
            }

            // 3. Confirm Uploads
            if (successUrls.length > 0) {
                setUploadProgress('Finalizing...');
                const confirmResponse = await api.confirmUpload(listingId, successUrls);

                if (confirmResponse.success) {
                    showToast.success(`${successUrls.length} file(s) uploaded successfully!`);

                    if (errors.length > 0) {
                        showToast.error(`Failed to upload: ${errors.join(', ')}`);
                    }

                    await fetchListings();
                } else {
                    showToast.error('Failed to confirm uploads');
                }
            } else if (errors.length > 0) {
                showToast.error('All uploads failed');
            }

        } catch (err) {
            console.error('Upload process error:', err);
            showToast.error(handleApiError(err, 'Upload process failed'));
        }

        setUploadingId(null);
        setUploadProgress('');
    };

    const handleFileDelete = async (listingId, fileUrl) => {
        if (!window.confirm('Delete this image?')) return;

        const fileName = fileUrl.split('/').pop();
        try {
            const response = await api.deleteParkingFile(listingId, fileName);
            if (response.success) {
                showToast.success('File deleted');
                await fetchListings();
            } else {
                showToast.error(response.message || 'Delete failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to delete file'));
        }
    };

    const validatePostalCode = async () => {
        if (form.postalCode && form.postalCode.length < 6) {
            showToast.error('Postal code must be 6 digits');
            // Optional: clear or keep focus depending on UX preference
            return;
        }

        if (form.postalCode.length === 6) {
            try {
                const response = await fetch(`https://api.postalpincode.in/pincode/${form.postalCode}`);
                const data = await response.json();

                if (data && data[0] && data[0].Status === 'Success') {
                    const postOffice = data[0].PostOffice[0];
                    const apiState = postOffice.State;
                    const apiDistrict = postOffice.District;
                    // const apiBlock = postOffice.Block; // May contain city name

                    // Verify State (Exact match usually works for states)
                    if (apiState !== form.state) {
                        showToast.error(`Postal code belongs to ${apiState}, not ${form.state}`);
                        setForm(prev => ({ ...prev, postalCode: '' }));
                        return;
                    }

                    // Verify City (Fuzzy match as district names might vary)
                    // Check if city name is part of district or vice versa
                    const cityLower = form.city.toLowerCase();
                    const districtLower = apiDistrict.toLowerCase();

                    if (!districtLower.includes(cityLower) && !cityLower.includes(districtLower)) {
                        showToast.error(`Postal code belongs to ${apiDistrict}, not ${form.city}`);
                        setForm(prev => ({ ...prev, postalCode: '' }));
                        return;
                    }

                    showToast.success('Postal code verified');
                } else {
                    showToast.error('Invalid Postal Code');
                    setForm(prev => ({ ...prev, postalCode: '' }));
                }
            } catch (error) {
                console.error('PIN verification failed:', error);
                // Don't block user if API fails, just log
            }
        }
    };

    return (
        <div className="page">
            <div className="container">
                <div className="flex-between mb-3">
                    <h1>My Parking Listings</h1>
                    <button
                        className="btn btn-primary"
                        onClick={() => {
                            const newShowForm = !showForm;
                            setForm(emptyForm);
                            setEditingId(null);
                            setShowForm(newShowForm);
                            if (newShowForm) {
                                setTimeout(() => {
                                    if (titleInputRef.current) {
                                        titleInputRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' });
                                        titleInputRef.current.focus();
                                    }
                                }, 100);
                            }
                        }}
                    >
                        {showForm ? 'Cancel' : '+ Add Listing'}
                    </button>
                </div>

                {showForm && (
                    <div className="card mb-3">
                        <h3 className="card-title mb-2">{editingId ? 'Edit Listing' : 'Create New Listing'}</h3>
                        <form onSubmit={handleSubmit}>
                            <div className="grid grid-2" style={{ gap: '1rem' }}>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Title *</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        ref={titleInputRef}
                                        value={form.title}
                                        onChange={(e) => setForm({ ...form, title: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Listing type</label>
                                    <select
                                        className="form-select"
                                        value={form.listingCategory}
                                        onChange={(e) => {
                                            const listingCategory = parseInt(e.target.value, 10) || 0;
                                            const residential = listingCategory === 1;
                                            setForm({
                                                ...form,
                                                listingCategory,
                                                instantBook: residential ? true : form.instantBook,
                                                totalSpots: residential && Number(form.totalSpots) > 10 ? 1 : (residential && !editingId ? 1 : form.totalSpots),
                                                parkingType: residential && !editingId ? 0 : form.parkingType,
                                            });
                                        }}
                                    >
                                        <option value={0}>Commercial lot / garage</option>
                                        <option value={1}>Residential driveway / home spot</option>
                                    </select>
                                </div>
                            </div>

                            <div className="grid grid-2 mt-1" style={{ gap: '1rem' }}>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Parking Type</label>
                                    <select
                                        className="form-select"
                                        value={form.parkingType}
                                        onChange={(e) => setForm({ ...form, parkingType: e.target.value })}
                                    >
                                        {PARKING_TYPES.map((type, i) => (
                                            <option key={i} value={i}>{type}</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="form-group" style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '0.5rem', paddingTop: '1.5rem' }}>
                                    <input
                                        type="checkbox"
                                        id="instantBook"
                                        checked={!!form.instantBook}
                                        onChange={(e) => setForm({ ...form, instantBook: e.target.checked })}
                                    />
                                    <label htmlFor="instantBook" className="form-label" style={{ margin: 0 }}>
                                        Instant book (skip host approval)
                                    </label>
                                </div>
                            </div>
                            {Number(form.listingCategory) === 1 && (
                                <p style={{ fontSize: '0.8rem', color: '#94a3b8', marginTop: '0.5rem' }}>
                                    Residential listings are limited to 10 spots. Guests book like a driveway rental (JustPark-style).
                                </p>
                            )}

                            <div className="form-group mt-1">
                                <label className="form-label">Description</label>
                                <textarea
                                    className="form-input"
                                    rows="3"
                                    value={form.description}
                                    onChange={(e) => setForm({ ...form, description: e.target.value })}
                                />
                            </div>

                            <div className="grid grid-4" style={{ gap: '1rem' }}>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Address *</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        value={form.address}
                                        onChange={(e) => setForm({ ...form, address: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label" style={{ display: 'block', marginBottom: '0.5rem' }}>State *</label>
                                    <select
                                        className="form-select"
                                        style={{ width: '100%' }}
                                        value={form.state}
                                        onChange={(e) => {
                                            const newState = e.target.value;
                                            setForm({ ...form, state: newState, city: '' });
                                        }}
                                        required
                                    >
                                        <option value="">Select State</option>
                                        {STATES.map(state => (
                                            <option key={state} value={state}>{state}</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label" style={{ display: 'block', marginBottom: '0.5rem' }}>City *</label>
                                    <select
                                        className="form-select"
                                        style={{ width: '100%' }}
                                        value={form.city}
                                        onChange={(e) => setForm({ ...form, city: e.target.value })}
                                        required
                                        disabled={!form.state}
                                    >
                                        <option value="">Select City</option>
                                        {form.state && INDIAN_STATES_CITIES[form.state]?.map(city => (
                                            <option key={city} value={city}>{city}</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label" style={{ display: 'block', marginBottom: '0.5rem' }}>Postal Code *</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        style={{ width: '100%' }}
                                        value={form.postalCode}
                                        onChange={(e) => {
                                            const val = e.target.value;
                                            // Only allow digits and max 6 characters
                                            if (/^\d{0,6}$/.test(val)) {
                                                setForm({ ...form, postalCode: val });
                                            }
                                        }}
                                        onBlur={validatePostalCode}
                                        required
                                        placeholder="6 digits"
                                    />
                                </div>
                            </div>

                            <Suspense fallback={<div className="loading" style={{ minHeight: '220px', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><div className="spinner" /></div>}>
                                <LocationPicker
                                    latitude={form.latitude}
                                    longitude={form.longitude}
                                    onLocationSelect={(lat, lng) => setForm(prev => ({ ...prev, latitude: lat, longitude: lng }))}
                                />
                            </Suspense>

                            <div className="grid grid-4 mt-1" style={{ gap: '1rem' }}>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Total Spots *</label>
                                    <input
                                        type="number"
                                        className="form-input"
                                        value={form.totalSpots}
                                        onChange={(e) => setForm({ ...form, totalSpots: e.target.value })}
                                        required
                                        min="1"
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Hourly Rate (₹) *</label>
                                    <input
                                        type="number"
                                        className="form-input"
                                        value={form.hourlyRate}
                                        onChange={(e) => setForm({ ...form, hourlyRate: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Daily Rate (₹) *</label>
                                    <input
                                        type="number"
                                        className="form-input"
                                        value={form.dailyRate}
                                        onChange={(e) => setForm({ ...form, dailyRate: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Monthly Rate (₹) *</label>
                                    <input
                                        type="number"
                                        className="form-input"
                                        value={form.monthlyRate}
                                        onChange={(e) => setForm({ ...form, monthlyRate: e.target.value })}
                                        required
                                    />
                                </div>
                            </div>

                            <div className="form-group mt-1">
                                <label className="form-label">Amenities (comma-separated)</label>
                                <input
                                    type="text"
                                    className="form-input"
                                    placeholder="CCTV, Security, Covered, EV Charging"
                                    value={form.amenities}
                                    onChange={(e) => setForm({ ...form, amenities: e.target.value })}
                                />
                            </div>

                            <div className="form-group" style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={form.is24Hours}
                                        onChange={(e) => setForm({ ...form, is24Hours: e.target.checked })}
                                    />
                                    24/7 Available
                                </label>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={!!form.isLprEnabled}
                                        onChange={(e) => setForm({ ...form, isLprEnabled: e.target.checked })}
                                    />
                                    Enable LPR (ticketless gate access — requires plate on booking)
                                </label>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={!!form.isDynamicPricingEnabled}
                                        onChange={(e) => setForm({ ...form, isDynamicPricingEnabled: e.target.checked })}
                                    />
                                    Enable dynamic pricing (demand-based rates from occupancy, peak hours, weekends)
                                </label>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={!!form.hasEvCharging}
                                        onChange={(e) => setForm({ ...form, hasEvCharging: e.target.checked })}
                                    />
                                    Enable EV charging (bays + session/idle rates)
                                </label>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={!!form.isBayGuidanceEnabled}
                                        onChange={(e) => setForm({ ...form, isBayGuidanceEnabled: e.target.checked })}
                                    />
                                    Indoor bay guidance (assign level / zone / bay on booking)
                                </label>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={!!form.isValetEnabled}
                                        onChange={(e) => setForm({ ...form, isValetEnabled: e.target.checked })}
                                    />
                                    Valet vehicle retrieval (guest can request ~10 min before pickup)
                                </label>
                                {(form.isBayGuidanceEnabled || form.isValetEnabled) && (
                                    <div className="grid grid-2" style={{ gap: '0.75rem', marginTop: '0.25rem' }}>
                                        {form.isBayGuidanceEnabled && (
                                            <>
                                                <div className="form-group" style={{ margin: 0 }}>
                                                    <label className="form-label">Default level</label>
                                                    <input
                                                        type="text"
                                                        className="form-input"
                                                        placeholder="e.g. P2"
                                                        value={form.defaultFacilityLevel}
                                                        onChange={(e) => setForm({ ...form, defaultFacilityLevel: e.target.value })}
                                                    />
                                                </div>
                                                <div className="form-group" style={{ margin: 0 }}>
                                                    <label className="form-label">Default zone</label>
                                                    <input
                                                        type="text"
                                                        className="form-input"
                                                        placeholder="e.g. Blue"
                                                        value={form.defaultFacilityZone}
                                                        onChange={(e) => setForm({ ...form, defaultFacilityZone: e.target.value })}
                                                    />
                                                </div>
                                                <div className="form-group" style={{ margin: 0, gridColumn: '1 / -1' }}>
                                                    <label className="form-label">Indoor wayfinding notes</label>
                                                    <textarea
                                                        className="form-input"
                                                        rows={2}
                                                        placeholder="Enter ramp B → elevators → follow blue signs to bay"
                                                        value={form.indoorGuidanceNotes}
                                                        onChange={(e) => setForm({ ...form, indoorGuidanceNotes: e.target.value })}
                                                    />
                                                </div>
                                            </>
                                        )}
                                    </div>
                                )}
                                {form.hasEvCharging && (
                                    <div className="grid grid-2" style={{ gap: '0.75rem', marginTop: '0.25rem' }}>
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Charger bays</label>
                                            <input
                                                type="number"
                                                className="form-input"
                                                min="0"
                                                value={form.evChargerCount}
                                                onChange={(e) => setForm({ ...form, evChargerCount: e.target.value })}
                                            />
                                        </div>
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Pricing mode</label>
                                            <select
                                                className="form-select"
                                                value={form.evPricingMode ?? 0}
                                                onChange={(e) => setForm({ ...form, evPricingMode: parseInt(e.target.value, 10) })}
                                            >
                                                <option value={0}>Hourly (lock at book)</option>
                                                <option value={1}>Per kWh (settle after charge)</option>
                                            </select>
                                        </div>
                                        {Number(form.evPricingMode) === 1 ? (
                                            <div className="form-group" style={{ margin: 0 }}>
                                                <label className="form-label">Energy rate ₹/kWh</label>
                                                <input
                                                    type="number"
                                                    className="form-input"
                                                    min="0"
                                                    step="0.5"
                                                    value={form.evRatePerKwh}
                                                    onChange={(e) => setForm({ ...form, evRatePerKwh: e.target.value })}
                                                />
                                            </div>
                                        ) : (
                                            <div className="form-group" style={{ margin: 0 }}>
                                                <label className="form-label">Charging rate ₹/hr</label>
                                                <input
                                                    type="number"
                                                    className="form-input"
                                                    min="0"
                                                    step="1"
                                                    value={form.evChargingRatePerHour}
                                                    onChange={(e) => setForm({ ...form, evChargingRatePerHour: e.target.value })}
                                                />
                                            </div>
                                        )}
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Idle fee ₹/hr</label>
                                            <input
                                                type="number"
                                                className="form-input"
                                                min="0"
                                                step="1"
                                                value={form.evIdleRatePerHour}
                                                onChange={(e) => setForm({ ...form, evIdleRatePerHour: e.target.value })}
                                            />
                                        </div>
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Idle grace (min)</label>
                                            <input
                                                type="number"
                                                className="form-input"
                                                min="0"
                                                value={form.evIdleGraceMinutes}
                                                onChange={(e) => setForm({ ...form, evIdleGraceMinutes: e.target.value })}
                                            />
                                        </div>
                                        <p style={{ gridColumn: '1 / -1', margin: 0, fontSize: '0.85rem', color: '#9ca3af' }}>
                                            {Number(form.evPricingMode) === 1
                                                ? 'Energy fee is settled after the charge session (OCPP/simulator). Idle fee still applies after end + grace.'
                                                : 'Hourly charging fee is added at booking. Idle fee applies after end + grace if the EV session overstays.'}
                                        </p>
                                    </div>
                                )}
                                {form.isDynamicPricingEnabled && (
                                    <div className="grid grid-2" style={{ gap: '0.75rem', marginTop: '0.25rem' }}>
                                        <div className="form-group" style={{ margin: 0, gridColumn: '1 / -1' }}>
                                            <label className="form-label">Peak / weekend timezone</label>
                                            <select
                                                className="form-select"
                                                value={form.timeZoneId || 'UTC'}
                                                onChange={(e) => setForm({ ...form, timeZoneId: e.target.value })}
                                            >
                                                <option value="Asia/Kolkata">Asia/Kolkata (IST)</option>
                                                <option value="UTC">UTC</option>
                                                <option value="Asia/Dubai">Asia/Dubai</option>
                                                <option value="Europe/London">Europe/London</option>
                                                <option value="America/New_York">America/New_York</option>
                                                <option value="America/Los_Angeles">America/Los_Angeles</option>
                                                <option value="Asia/Singapore">Asia/Singapore</option>
                                            </select>
                                            <p style={{ margin: '0.35rem 0 0', fontSize: '0.8rem', color: '#9ca3af' }}>
                                                Peak windows 07:00–10:00 and 16:00–20:00 use this local clock.
                                            </p>
                                        </div>
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Min multiplier</label>
                                            <input
                                                type="number"
                                                className="form-input"
                                                step="0.05"
                                                min="0.1"
                                                max="1"
                                                value={form.dynamicMinMultiplier}
                                                onChange={(e) => setForm({ ...form, dynamicMinMultiplier: e.target.value })}
                                            />
                                        </div>
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Max multiplier</label>
                                            <input
                                                type="number"
                                                className="form-input"
                                                step="0.05"
                                                min="1"
                                                max="5"
                                                value={form.dynamicMaxMultiplier}
                                                onChange={(e) => setForm({ ...form, dynamicMaxMultiplier: e.target.value })}
                                            />
                                        </div>
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Peak hour multiplier</label>
                                            <input
                                                type="number"
                                                className="form-input"
                                                step="0.05"
                                                min="1"
                                                max="3"
                                                value={form.peakHourMultiplier}
                                                onChange={(e) => setForm({ ...form, peakHourMultiplier: e.target.value })}
                                            />
                                        </div>
                                        <div className="form-group" style={{ margin: 0 }}>
                                            <label className="form-label">Weekend multiplier</label>
                                            <input
                                                type="number"
                                                className="form-input"
                                                step="0.05"
                                                min="1"
                                                max="3"
                                                value={form.weekendMultiplier}
                                                onChange={(e) => setForm({ ...form, weekendMultiplier: e.target.value })}
                                            />
                                        </div>
                                        <p style={{ gridColumn: '1 / -1', margin: 0, fontSize: '0.85rem', color: '#9ca3af' }}>
                                            Peak windows (UTC): 07:00–10:00 and 16:00–20:00. Occupancy surge/discount is automatic from available spots.
                                        </p>
                                    </div>
                                )}
                            </div>

                            <button type="submit" className="btn btn-primary">
                                {editingId ? 'Update Listing' : 'Create Listing'}
                            </button>
                        </form>
                    </div>
                )}

                {loading ? (
                    <div className="loading">
                        <div className="spinner"></div>
                    </div>
                ) : listings.length === 0 ? (
                    <div className="empty-state">
                        <div className="empty-icon">🅿️</div>
                        <h3>No listings yet</h3>
                        <p>Create your first parking listing to start earning</p>
                    </div>
                ) : (
                    <div className="grid grid-2">
                        {listings.map(listing => (
                            <div key={listing.id} className="card hover-card">
                                <div className="flex-between">
                                    <h3 className="card-title">{listing.title}</h3>
                                    <span className={`parking-tag ${listing.isActive ? '' : 'inactive'}`}
                                        style={{ background: listing.isActive ? 'rgba(16, 185, 129, 0.2)' : 'rgba(107, 114, 128, 0.2)' }}>
                                        {listing.isActive ? 'Active' : 'Inactive'}
                                    </span>
                                    {listing.isLprEnabled && (
                                        <span className="parking-tag"
                                            style={{ background: 'rgba(99, 102, 241, 0.2)', color: '#a5b4fc' }}>
                                            LPR
                                        </span>
                                    )}
                                    {listing.isDynamicPricingEnabled && (
                                        <span className="parking-tag"
                                            style={{ background: 'rgba(245, 158, 11, 0.2)', color: '#fbbf24' }}>
                                            Dynamic $
                                        </span>
                                    )}
                                    {listing.isBayGuidanceEnabled && (
                                        <span className="badge" style={{ background: 'rgba(59,130,246,0.15)', color: '#60a5fa' }}>Bay guidance</span>
                                    )}
                                    {listing.isValetEnabled && (
                                        <span className="badge" style={{ background: 'rgba(168,85,247,0.15)', color: '#c084fc' }}>Valet</span>
                                    )}
                                    {listing.hasEvCharging && (
                                        <span className="parking-tag"
                                            style={{ background: 'rgba(16, 185, 129, 0.2)', color: '#6ee7b7' }}>
                                            🔌 EV
                                        </span>
                                    )}
                                    {(listing.listingCategory === 1 || listing.listingCategory === 'Residential') && (
                                        <span className="parking-tag"
                                            style={{ background: 'rgba(236, 72, 153, 0.2)', color: '#f9a8d4' }}>
                                            🏠 Driveway
                                        </span>
                                    )}
                                    {listing.instantBook && (
                                        <span className="parking-tag"
                                            style={{ background: 'rgba(34, 197, 94, 0.2)', color: '#86efac' }}>
                                            Instant book
                                        </span>
                                    )}
                                </div>
                                <div className="parking-location">📍 {listing.address}, {listing.city}</div>
                                <div className="flex gap-2 mt-1">
                                    <span className="parking-tag">{PARKING_TYPES[listing.parkingType]}</span>
                                    <span className="parking-tag">{listing.totalSpots} spots</span>
                                    <span className="rating">⭐ {listing.averageRating?.toFixed(1) || 'New'}</span>
                                </div>
                                <div className="parking-price mt-1">
                                    {listing.dynamicPricingApplied && listing.effectiveHourlyRate != null
                                        && Number(listing.effectiveHourlyRate) !== Number(listing.hourlyRate) ? (
                                        <>
                                            <span style={{ fontSize: '0.75rem', color: '#94a3b8', marginRight: '0.35rem' }}>from</span>
                                            ₹{Number(listing.effectiveHourlyRate).toFixed(0)}
                                            <span>/hr</span>
                                            <span style={{
                                                marginLeft: '0.4rem',
                                                fontSize: '0.75rem',
                                                color: '#94a3b8',
                                                textDecoration: 'line-through',
                                            }}>
                                                ₹{listing.hourlyRate}
                                            </span>
                                        </>
                                    ) : (
                                        <>₹{listing.hourlyRate}<span>/hr</span></>
                                    )}
                                </div>

                                {/* Ancillary add-ons catalog */}
                                <div style={{
                                    marginTop: '0.85rem',
                                    padding: '0.75rem',
                                    background: 'rgba(244, 114, 182, 0.08)',
                                    borderRadius: 'var(--radius-sm)',
                                    border: '1px solid rgba(244, 114, 182, 0.25)',
                                }}>
                                    <button
                                        type="button"
                                        className="btn btn-secondary"
                                        style={{ width: '100%', marginBottom: ancillaryOpenId === listing.id ? '0.75rem' : 0 }}
                                        onClick={() => setAncillaryOpenId(prev => prev === listing.id ? null : listing.id)}
                                    >
                                        🧼 Add-on services ({(ancillaryBySpace[listing.id] || []).length})
                                        {' '}{ancillaryOpenId === listing.id ? '▲' : '▼'}
                                    </button>
                                    {ancillaryOpenId === listing.id && (
                                        <div>
                                            {(ancillaryBySpace[listing.id] || []).length === 0 ? (
                                                <p style={{ fontSize: '0.85rem', color: 'var(--color-text-secondary)', margin: '0 0 0.75rem' }}>
                                                    No add-ons yet. Guests can pick these at booking (wash, detail, etc.).
                                                </p>
                                            ) : (
                                                <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 0.75rem' }}>
                                                    {(ancillaryBySpace[listing.id] || []).map(svc => (
                                                        <li key={svc.id} style={{
                                                            display: 'flex',
                                                            justifyContent: 'space-between',
                                                            alignItems: 'center',
                                                            gap: '0.5rem',
                                                            padding: '0.4rem 0',
                                                            borderBottom: '1px solid rgba(255,255,255,0.06)',
                                                            fontSize: '0.9rem',
                                                        }}>
                                                            <div>
                                                                <strong>{svc.name}</strong>
                                                                <span style={{ marginLeft: '0.5rem', color: 'var(--color-primary)' }}>
                                                                    ₹{svc.price}
                                                                </span>
                                                                {!svc.isActive && (
                                                                    <span style={{ marginLeft: '0.4rem', fontSize: '0.75rem', color: '#9ca3af' }}>
                                                                        (inactive)
                                                                    </span>
                                                                )}
                                                                {svc.durationMinutes ? (
                                                                    <span style={{ marginLeft: '0.4rem', fontSize: '0.75rem', color: '#9ca3af' }}>
                                                                        · {svc.durationMinutes} min
                                                                    </span>
                                                                ) : null}
                                                            </div>
                                                            <div style={{ display: 'flex', gap: '0.35rem' }}>
                                                                <button
                                                                    type="button"
                                                                    className="btn btn-secondary"
                                                                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.75rem' }}
                                                                    onClick={() => handleToggleAncillaryActive(svc)}
                                                                >
                                                                    {svc.isActive ? 'Hide' : 'Show'}
                                                                </button>
                                                                {svc.isActive && (
                                                                    <button
                                                                        type="button"
                                                                        className="btn btn-secondary"
                                                                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.75rem' }}
                                                                        onClick={() => handleDeactivateAncillary(svc.id)}
                                                                    >
                                                                        Deactivate
                                                                    </button>
                                                                )}
                                                            </div>
                                                        </li>
                                                    ))}
                                                </ul>
                                            )}
                                            <div className="grid grid-2" style={{ gap: '0.5rem' }}>
                                                <input
                                                    className="form-input"
                                                    placeholder="Name (e.g. Basic wash)"
                                                    value={(ancillaryForm[listing.id] || emptyAncillaryDraft).name}
                                                    onChange={(e) => setAncillaryForm(prev => ({
                                                        ...prev,
                                                        [listing.id]: { ...(prev[listing.id] || emptyAncillaryDraft), name: e.target.value },
                                                    }))}
                                                />
                                                <input
                                                    className="form-input"
                                                    type="number"
                                                    min="0"
                                                    step="0.01"
                                                    placeholder="Price ₹"
                                                    value={(ancillaryForm[listing.id] || emptyAncillaryDraft).price}
                                                    onChange={(e) => setAncillaryForm(prev => ({
                                                        ...prev,
                                                        [listing.id]: { ...(prev[listing.id] || emptyAncillaryDraft), price: e.target.value },
                                                    }))}
                                                />
                                                <input
                                                    className="form-input"
                                                    placeholder="Description (optional)"
                                                    value={(ancillaryForm[listing.id] || emptyAncillaryDraft).description}
                                                    onChange={(e) => setAncillaryForm(prev => ({
                                                        ...prev,
                                                        [listing.id]: { ...(prev[listing.id] || emptyAncillaryDraft), description: e.target.value },
                                                    }))}
                                                />
                                                <input
                                                    className="form-input"
                                                    type="number"
                                                    min="0"
                                                    placeholder="Duration min (opt.)"
                                                    value={(ancillaryForm[listing.id] || emptyAncillaryDraft).durationMinutes}
                                                    onChange={(e) => setAncillaryForm(prev => ({
                                                        ...prev,
                                                        [listing.id]: { ...(prev[listing.id] || emptyAncillaryDraft), durationMinutes: e.target.value },
                                                    }))}
                                                />
                                            </div>
                                            <button
                                                type="button"
                                                className="btn btn-primary"
                                                style={{ marginTop: '0.5rem', width: '100%' }}
                                                disabled={ancillarySaving === listing.id}
                                                onClick={() => handleCreateAncillary(listing.id)}
                                            >
                                                {ancillarySaving === listing.id ? 'Saving…' : 'Add service'}
                                            </button>
                                        </div>
                                    )}
                                </div>

                                {/* Active Reservations */}
                                {(() => {
                                    const activeBookings = getActiveBookingsForListing(listing.id, listing.activeReservations);
                                    if (activeBookings.length === 0) return null;
                                    return (
                                        <div style={{
                                            marginTop: '1rem',
                                            padding: '0.75rem',
                                            background: 'rgba(59, 130, 246, 0.1)',
                                            borderRadius: 'var(--radius-sm)',
                                            border: '1px solid rgba(59, 130, 246, 0.3)'
                                        }}>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
                                                <span style={{ fontSize: '0.9rem' }}>📅</span>
                                                <strong style={{ color: 'var(--color-primary)', fontSize: '0.85rem' }}>
                                                    Active Reservations ({activeBookings.length})
                                                </strong>
                                            </div>
                                            <div style={{ maxHeight: '260px', overflowY: 'auto' }}>
                                                {activeBookings.map(booking => (
                                                    <div key={booking.id} style={{
                                                        padding: '0.5rem',
                                                        marginBottom: '0.4rem',
                                                        background: 'rgba(0,0,0,0.2)',
                                                        borderRadius: '6px',
                                                        fontSize: '0.8rem'
                                                    }}>
                                                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.3rem' }}>
                                                            <span style={{
                                                                padding: '2px 6px',
                                                                borderRadius: '4px',
                                                                fontSize: '0.7rem',
                                                                ...getStatusStyle(booking.status)
                                                            }}>
                                                                {getStatusLabel(booking.status)}
                                                            </span>
                                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem' }}>
                                                                {formatDateTime(booking.startDateTime)} → {formatDateTime(booking.endDateTime)}
                                                            </span>
                                                        </div>
                                                        <div style={{ display: 'flex', gap: '0.75rem', marginTop: '0.3rem', flexWrap: 'wrap', alignItems: 'center' }}>
                                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                                                                <span style={{ fontSize: '0.85rem' }}>👤</span>
                                                                <span style={{ fontWeight: '500' }}>{booking.userName || 'Unknown'}</span>
                                                            </div>
                                                            {booking.vehicleNumber && (
                                                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                                                                    <span style={{ fontSize: '0.85rem' }}>🚗</span>
                                                                    <span>{booking.vehicleNumber}</span>
                                                                    {booking.vehicleModel && <span style={{ color: 'var(--color-text-muted)' }}>({booking.vehicleModel})</span>}
                                                                </div>
                                                            )}
                                                            {booking.slotNumber && (
                                                                <span style={{
                                                                    display: 'inline-flex',
                                                                    alignItems: 'center',
                                                                    gap: '0.2rem',
                                                                    background: 'rgba(99,102,241,0.15)',
                                                                    color: '#818cf8',
                                                                    border: '1px solid rgba(99,102,241,0.35)',
                                                                    borderRadius: '5px',
                                                                    padding: '1px 6px',
                                                                    fontWeight: 600,
                                                                    fontSize: '0.75rem',
                                                                }}>🅿️ P{booking.slotNumber}</span>
                                                            )}
                                                        </div>
                                                    </div>
                                                ))}
                                            </div>
                                        </div>
                                    );
                                })()}

                                {/* Image Gallery */}
                                {listing.imageUrls && listing.imageUrls.length > 0 && (
                                    <div style={{ marginTop: '1rem' }}>
                                        <small style={{ color: 'var(--color-text-muted)' }}>Images ({listing.imageUrls.length})</small>
                                        <div style={{
                                            display: 'flex',
                                            gap: '0.5rem',
                                            flexWrap: 'wrap',
                                            marginTop: '0.5rem'
                                        }}>
                                            {listing.imageUrls.map((url, i) => (
                                                <div key={i} style={{ position: 'relative' }}>
                                                    <img
                                                        src={url.startsWith('http') ? url : `${API_BASE}${url}`}
                                                        alt={`Parking ${i + 1}`}
                                                        style={{
                                                            width: '60px',
                                                            height: '60px',
                                                            objectFit: 'cover',
                                                            borderRadius: 'var(--radius-sm)',
                                                            cursor: 'pointer'
                                                        }}
                                                        loading="lazy"
                                                    />
                                                    <button
                                                        onClick={(e) => { e.stopPropagation(); handleFileDelete(listing.id, url); }}
                                                        style={{
                                                            position: 'absolute',
                                                            top: '-6px',
                                                            right: '-6px',
                                                            width: '18px',
                                                            height: '18px',
                                                            borderRadius: '50%',
                                                            background: '#ef4444',
                                                            color: 'white',
                                                            border: 'none',
                                                            cursor: 'pointer',
                                                            fontSize: '12px',
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'center'
                                                        }}
                                                    >×</button>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                )}

                                {/* Upload Section */}
                                <div style={{ marginTop: '1rem' }}>
                                    <label
                                        style={{
                                            display: 'block',
                                            padding: '0.75rem',
                                            border: '2px dashed var(--color-border)',
                                            borderRadius: 'var(--radius-sm)',
                                            textAlign: 'center',
                                            cursor: uploadingId === listing.id ? 'wait' : 'pointer',
                                            transition: 'border-color 0.2s',
                                            background: 'rgba(99, 102, 241, 0.05)'
                                        }}
                                        onDragOver={(e) => { e.preventDefault(); e.currentTarget.style.borderColor = 'var(--color-primary)'; }}
                                        onDragLeave={(e) => { e.currentTarget.style.borderColor = 'var(--color-border)'; }}
                                        onDrop={(e) => {
                                            e.preventDefault();
                                            e.currentTarget.style.borderColor = 'var(--color-border)';
                                            handleFileUpload(listing.id, e.dataTransfer.files);
                                        }}
                                    >
                                        <input
                                            type="file"
                                            multiple
                                            accept="image/jpeg,image/png,image/webp,video/mp4,video/webm"
                                            style={{ display: 'none' }}
                                            onChange={(e) => handleFileUpload(listing.id, e.target.files)}
                                            disabled={uploadingId === listing.id}
                                        />
                                        {uploadingId === listing.id ? (
                                            <span>⏳ {uploadProgress}</span>
                                        ) : (
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.85rem' }}>
                                                📷 Drop images/videos or click to upload
                                            </span>
                                        )}
                                    </label>
                                    <small style={{ color: 'var(--color-text-muted)', display: 'block', marginTop: '0.25rem' }}>
                                        Max: 5MB images, 50MB videos
                                    </small>
                                </div>

                                <div className="flex gap-1 mt-2" style={{ flexWrap: 'wrap' }}>
                                    <button className="btn btn-secondary" onClick={() => handleEdit(listing)}>Edit</button>
                                    <button className="btn btn-danger" onClick={() => handleDelete(listing.id)}>Delete</button>
                                    <Link to={`/parking/${listing.id}`} className="btn btn-outline">View</Link>
                                    {listing.isLprEnabled && (
                                        <Link to={`/my/listings/${listing.id}/lpr`} className="btn btn-outline">
                                            LPR registry
                                        </Link>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}

