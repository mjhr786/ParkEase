import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';
import { handleApiError } from '../utils/errorHandler';
import showToast from '../utils/toast.jsx';
import INDIAN_STATES_CITIES, { STATES } from '../utils/indianStatesCities';
import LocationPicker from '../components/LocationPicker';

import { API_BASE_URL } from '../config';

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];
const API_BASE = API_BASE_URL;

// Notification types that should trigger a refresh of vendor listings
const REFRESH_TRIGGERS = [
    'booking.requested',   // New booking request
    'payment.completed',   // User paid
    'booking.cancelled',   // User cancelled
    'booking.checkin',     // User checked in
    'booking.checkout'     // User checked out
];

export default function VendorListings() {
    const [listings, setListings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showForm, setShowForm] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [uploadingId, setUploadingId] = useState(null);
    const [uploadProgress, setUploadProgress] = useState('');
    const [bookings, setBookings] = useState([]);

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
        totalSpots: 10,
        hourlyRate: 50,
        dailyRate: 400,
        weeklyRate: 2500,
        monthlyRate: 8000,
        is24Hours: true,
        amenities: '',
        specialInstructions: '',
    };

    const [form, setForm] = useState(emptyForm);

    const fetchListings = useCallback(async () => {
        try {
            const response = await api.getMyListings();
            if (response.success && response.data) {
                setListings(response.data);
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to load listings'));
        }
        setLoading(false);
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
        fetchListings();
        fetchBookings();
    }, [fetchListings, fetchBookings]);

    // Subscribe to real-time refresh events
    useEffect(() => {
        const unsubscribe = subscribeToRefresh('VendorListings', REFRESH_TRIGGERS, () => {
            console.log('🔄 VendorListings: Auto-refreshing due to notification');
            fetchListings();
            fetchBookings();
        });
        return unsubscribe;
    }, [subscribeToRefresh, fetchListings, fetchBookings]);

    const getActiveBookingsForListing = (listingId) => {
        const now = new Date();
        // Status values: 0=Pending, 1=Confirmed, 2=InProgress, 6=AwaitingPayment
        const activeStatuses = [0, 1, 2, 6, 'Pending', 'Confirmed', 'InProgress', 'AwaitingPayment'];
        return bookings.filter(b =>
            b.parkingSpaceId === listingId &&
            activeStatuses.includes(b.status) &&
            new Date(b.endDateTime) > now
        ).sort((a, b) => new Date(a.startDateTime) - new Date(b.startDateTime));
    };

    const getStatusLabel = (status) => {
        const labels = { 0: 'Pending', 1: 'Confirmed', 2: 'InProgress', 6: 'AwaitingPayment' };
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
                fetchListings();
            } else {
                showToast.error(response.message || 'Operation failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to save listing'));
        }
    };

    const handleEdit = (listing) => {
        setForm({
            ...listing,
            amenities: listing.amenities?.join(', ') || '',
        });
        setEditingId(listing.id);
        setShowForm(true);
    };

    const handleDelete = async (id) => {
        if (!window.confirm('Are you sure you want to delete this listing?')) return;

        try {
            const response = await api.deleteParking(id);
            if (response.success) {
                showToast.success('Listing deleted successfully!');
                fetchListings();
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
        setUploadProgress('Uploading...');

        try {
            const response = await api.uploadParkingFiles(listingId, Array.from(files));
            if (response.success) {
                const count = response.data.urls.length;
                showToast.success(`${count} file(s) uploaded successfully!`);
                if (response.data.errors?.length > 0) {
                    showToast.error(`Some files skipped: ${response.data.errors.join(', ')}`);
                }
                fetchListings();
            } else {
                showToast.error(response.message || 'Upload failed');
            }
        } catch (err) {
            showToast.error(handleApiError(err, 'Failed to upload files'));
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
                fetchListings();
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
                            setForm(emptyForm);
                            setEditingId(null);
                            setShowForm(!showForm);
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
                                        value={form.title}
                                        onChange={(e) => setForm({ ...form, title: e.target.value })}
                                        required
                                    />
                                </div>
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
                            </div>

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

                            <LocationPicker
                                latitude={form.latitude}
                                longitude={form.longitude}
                                onLocationSelect={(lat, lng) => setForm(prev => ({ ...prev, latitude: lat, longitude: lng }))}
                            />

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

                            <div className="form-group">
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                                    <input
                                        type="checkbox"
                                        checked={form.is24Hours}
                                        onChange={(e) => setForm({ ...form, is24Hours: e.target.checked })}
                                    />
                                    24/7 Available
                                </label>
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
                            <div key={listing.id} className="card">
                                <div className="flex-between">
                                    <h3 className="card-title">{listing.title}</h3>
                                    <span className={`parking-tag ${listing.isActive ? '' : 'inactive'}`}
                                        style={{ background: listing.isActive ? 'rgba(16, 185, 129, 0.2)' : 'rgba(107, 114, 128, 0.2)' }}>
                                        {listing.isActive ? 'Active' : 'Inactive'}
                                    </span>
                                </div>
                                <div className="parking-location">📍 {listing.address}, {listing.city}</div>
                                <div className="flex gap-2 mt-1">
                                    <span className="parking-tag">{PARKING_TYPES[listing.parkingType]}</span>
                                    <span className="parking-tag">{listing.totalSpots} spots</span>
                                    <span className="rating">⭐ {listing.averageRating?.toFixed(1) || 'New'}</span>
                                </div>
                                <div className="parking-price mt-1">₹{listing.hourlyRate}<span>/hr</span></div>

                                {/* Active Reservations */}
                                {(() => {
                                    const activeBookings = getActiveBookingsForListing(listing.id);
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
                                            <div style={{ maxHeight: '180px', overflowY: 'auto' }}>
                                                {activeBookings.slice(0, 5).map(booking => (
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
                                                        <div style={{ display: 'flex', gap: '0.75rem', marginTop: '0.3rem' }}>
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
                                                        </div>
                                                    </div>
                                                ))}
                                                {activeBookings.length > 5 && (
                                                    <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)', paddingTop: '0.3rem' }}>
                                                        +{activeBookings.length - 5} more...
                                                    </div>
                                                )}
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
                                                        src={`${API_BASE}${url}`}
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

                                <div className="flex gap-1 mt-2">
                                    <button className="btn btn-secondary" onClick={() => handleEdit(listing)}>Edit</button>
                                    <button className="btn btn-danger" onClick={() => handleDelete(listing.id)}>Delete</button>
                                    <Link to={`/parking/${listing.id}`} className="btn btn-outline">View</Link>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
