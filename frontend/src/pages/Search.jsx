import { useState, useEffect, useCallback } from 'react';
import { useSearchParams, Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import api from '../services/api';
import LocationMap from '../components/LocationMap';
import INDIAN_STATES_CITIES, { STATES } from '../utils/indianStatesCities';
import toast from 'react-hot-toast';

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];
const VEHICLE_TYPES = ['Car', 'Motorcycle', 'SUV', 'Truck', 'Van', 'Electric'];
import { API_BASE_URL } from '../config';

export default function Search() {
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const { isAuthenticated, user } = useAuth();
    const [parkingSpaces, setParkingSpaces] = useState([]);
    const [mapParkingSpaces, setMapParkingSpaces] = useState([]);
    const [loading, setLoading] = useState(true);
    const [hoveredId, setHoveredId] = useState(null);
    const [favorites, setFavorites] = useState([]);
    const [favoritesLoading, setFavoritesLoading] = useState(false);
    const [filters, setFilters] = useState({
        state: '',
        city: searchParams.get('city') || '',
        latitude: searchParams.get('lat') || '',
        longitude: searchParams.get('lng') || '',
        radiusKm: searchParams.get('lat') ? 5 : '',
        address: '',
        minPrice: '',
        maxPrice: '',
        parkingType: '',
        vehicleType: '',
        minRating: '',
        page: 1,
        pageSize: 12,
    });
    const [totalPages, setTotalPages] = useState(1);

    const fetchParkingSpaces = useCallback(async (searchFilters) => {
        setLoading(true);
        try {
            const filtersToUse = searchFilters || filters;
            const params = Object.fromEntries(
                Object.entries(filtersToUse).filter(([, v]) => v !== '')
            );

            // Fetch list and map data in parallel
            const [listResponse, mapResponse] = await Promise.all([
                api.searchParking(params),
                api.getMapParking(params)
            ]);

            if (listResponse.success && listResponse.data) {
                setParkingSpaces(listResponse.data.parkingSpaces || []);
                setTotalPages(listResponse.data.totalPages || 1);
            }

            if (mapResponse.success && mapResponse.data) {
                setMapParkingSpaces(mapResponse.data || []);
            }
        } catch (error) {
            console.error('Search error:', error);
        }
        setLoading(false);
    }, [filters]);

    const fetchFavorites = async () => {
        try {
            const res = await api.getMyFavorites();
            if (res.success && res.data) {
                setFavorites(res.data.map(f => f.id));
            }
        } catch (err) {
            console.error('Error fetching favorites:', err);
        }
    };

    const toggleFavorite = async (e, parkingId) => {
        e.preventDefault(); // Prevent navigating to details
        if (!isAuthenticated) {
            toast.error("Please log in to save favorites");
            navigate('/login');
            return;
        }

        if (favoritesLoading) return;
        setFavoritesLoading(true);

        try {
            const res = await api.toggleFavorite(parkingId);
            if (res.success) {
                if (res.data) {
                    setFavorites(prev => [...prev, parkingId]);
                    toast.success("Added to favorites");
                } else {
                    setFavorites(prev => prev.filter(id => id !== parkingId));
                    toast.success("Removed from favorites");
                }
            }
        } catch (err) {
            toast.error("Failed to update favorites");
        } finally {
            setFavoritesLoading(false);
        }
    };

    // Fetch on initial load (show all parking)
    useEffect(() => {
        fetchParkingSpaces(filters);
        if (isAuthenticated) {
            fetchFavorites();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isAuthenticated]);

    // Fetch when page changes
    useEffect(() => {
        if (filters.page > 1) {
            fetchParkingSpaces(filters);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [filters.page]);

    const handleSearch = (e) => {
        e.preventDefault();
        const newFilters = { ...filters, page: 1 };
        setFilters(newFilters);
        fetchParkingSpaces(newFilters);
    };

    const handleFilterChange = (key, value) => {
        setFilters(prev => ({ ...prev, [key]: value }));
    };

    const handleNearMeClick = () => {
        if (!navigator.geolocation) {
            toast.error("Geolocation is not supported by your browser");
            return;
        }

        const toastId = toast.loading("Getting your location...");
        navigator.geolocation.getCurrentPosition(
            (position) => {
                toast.dismiss(toastId);
                toast.success("Location found!");
                const newFilters = {
                    ...filters,
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                    radiusKm: 5,
                    page: 1,
                    city: '', // Clear city/state if using exact coordinates
                    state: '',
                    address: ''
                };
                setFilters(newFilters);
                fetchParkingSpaces(newFilters);
            },
            (error) => {
                toast.dismiss(toastId);
                let errorMessage = "Unable to retrieve your location";
                if (error.code === 1) errorMessage = "Location access denied. Please allow location access in your browser.";
                toast.error(errorMessage);
            },
            { enableHighAccuracy: true, timeout: 5000, maximumAge: 0 }
        );
    };

    return (
        <div className="page">
            <div className="container">
                <h1 style={{ marginBottom: '1.5rem' }}>Find Parking</h1>

                {/* Filters */}
                <div className="card hover-card mb-3">
                    <form onSubmit={handleSearch}>
                        <div className="grid grid-4" style={{ gap: '1rem' }}>
                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">State</label>
                                <select
                                    className="form-select"
                                    value={filters.state}
                                    onChange={(e) => {
                                        setFilters(prev => ({ ...prev, state: e.target.value, city: '' }));
                                    }}
                                >
                                    <option value="">Select State</option>
                                    {STATES.map(state => (
                                        <option key={state} value={state}>{state}</option>
                                    ))}
                                </select>
                            </div>

                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">City</label>
                                <select
                                    className="form-select"
                                    value={filters.city}
                                    onChange={(e) => handleFilterChange('city', e.target.value)}
                                    disabled={!filters.state}
                                >
                                    <option value="">Select City</option>
                                    {filters.state && INDIAN_STATES_CITIES[filters.state]?.map(city => (
                                        <option key={city} value={city}>{city}</option>
                                    ))}
                                </select>
                            </div>

                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">Address/Area</label>
                                <input
                                    type="text"
                                    className="form-input"
                                    placeholder="Street or area"
                                    value={filters.address}
                                    onChange={(e) => handleFilterChange('address', e.target.value)}
                                />
                            </div>

                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">Parking Type</label>
                                <select
                                    className="form-select"
                                    value={filters.parkingType}
                                    onChange={(e) => handleFilterChange('parkingType', e.target.value)}
                                >
                                    <option value="">All Types</option>
                                    {PARKING_TYPES.map(type => (
                                        <option key={type} value={type}>{type}</option>
                                    ))}
                                </select>
                            </div>

                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">Vehicle Type</label>
                                <select
                                    className="form-select"
                                    value={filters.vehicleType}
                                    onChange={(e) => handleFilterChange('vehicleType', e.target.value)}
                                >
                                    <option value="">All Vehicles</option>
                                    {VEHICLE_TYPES.map(type => (
                                        <option key={type} value={type}>{type}</option>
                                    ))}
                                </select>
                            </div>

                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">Min Price (₹/hr)</label>
                                <input
                                    type="number"
                                    className="form-input"
                                    placeholder="0"
                                    value={filters.minPrice}
                                    onChange={(e) => handleFilterChange('minPrice', e.target.value)}
                                />
                            </div>

                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">Max Price (₹/hr)</label>
                                <input
                                    type="number"
                                    className="form-input"
                                    placeholder="1000"
                                    value={filters.maxPrice}
                                    onChange={(e) => handleFilterChange('maxPrice', e.target.value)}
                                />
                            </div>

                            <div className="form-group" style={{ margin: 0 }}>
                                <label className="form-label">Min Rating</label>
                                <select
                                    className="form-select"
                                    value={filters.minRating}
                                    onChange={(e) => handleFilterChange('minRating', e.target.value)}
                                >
                                    <option value="">Any Rating</option>
                                    <option value="4">4+ Stars</option>
                                    <option value="3">3+ Stars</option>
                                    <option value="2">2+ Stars</option>
                                </select>
                            </div>

                            <div className="form-group" style={{ margin: 0, alignSelf: 'flex-end', display: 'flex', gap: '0.5rem' }}>
                                <button type="button" className="btn btn-secondary" onClick={handleNearMeClick} style={{ flex: '1 0 auto', padding: '0.875rem 0.5rem', whiteSpace: 'nowrap' }} title="Use my current location">
                                    📍 Near Me
                                </button>
                                <button type="submit" className="btn btn-primary" style={{ flex: '2 1 auto' }}>
                                    🔍 Search
                                </button>
                            </div>
                        </div>
                    </form>
                </div>

                {/* Split View: Cards + Map */}
                <div className="search-split">
                    {/* Left: Cards */}
                    <div className="search-cards">
                        {loading ? (
                            <div className="search-card-list">
                                {[1, 2, 3, 4, 5, 6].map(n => (
                                    <div key={n} className="skeleton-card" />
                                ))}
                            </div>
                        ) : parkingSpaces.length === 0 ? (
                            <div className="empty-state">
                                <div className="empty-icon">🅿️</div>
                                <h3>No parking spaces found</h3>
                                <p>Try adjusting your search filters</p>
                            </div>
                        ) : (
                            <>
                                <div className="search-results-count mb-1">
                                    <span style={{ color: 'var(--color-text-muted)', fontSize: '0.9rem' }}>
                                        {parkingSpaces.length} parking spot{parkingSpaces.length !== 1 ? 's' : ''} found
                                    </span>
                                </div>
                                <div className="search-card-list">
                                    {parkingSpaces.map(parking => (
                                        <Link
                                            to={`/parking/${parking.id}`}
                                            key={parking.id}
                                            style={{ textDecoration: 'none', color: 'inherit' }}
                                            onMouseEnter={() => setHoveredId(parking.id)}
                                            onMouseLeave={() => setHoveredId(null)}
                                        >
                                            <div className={`card parking-card hover-card ${hoveredId === parking.id ? 'parking-card-highlighted' : ''}`}>
                                                {parking.imageUrls && parking.imageUrls.length > 0 ? (
                                                    <img
                                                        src={parking.imageUrls[0].startsWith('http') ? parking.imageUrls[0] : `${API_BASE_URL}${parking.imageUrls[0]}`}
                                                        alt={parking.title}
                                                        className="parking-image"
                                                        style={{ objectFit: 'cover' }}
                                                    />
                                                ) : (
                                                    <div className="parking-image">🅿️</div>
                                                )}
                                                <button
                                                    className="favorite-btn"
                                                    onClick={(e) => toggleFavorite(e, parking.id)}
                                                    style={{
                                                        position: 'absolute',
                                                        top: '1rem',
                                                        right: '1rem',
                                                        background: 'rgba(255, 255, 255, 0.9)',
                                                        border: 'none',
                                                        borderRadius: '50%',
                                                        width: '36px',
                                                        height: '36px',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        justifyContent: 'center',
                                                        cursor: 'pointer',
                                                        boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
                                                        zIndex: 2,
                                                        fontSize: '1.2rem',
                                                        transition: 'transform 0.2s',
                                                    }}
                                                    onMouseEnter={(e) => e.currentTarget.style.transform = 'scale(1.1)'}
                                                    onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                                                >
                                                    {favorites.includes(parking.id) ? '❤️' : '🤍'}
                                                </button>
                                                <h3 className="card-title" style={{ marginTop: '0.5rem' }}>{parking.title}</h3>
                                                <div className="parking-location">
                                                    📍 {parking.address}, {parking.city}
                                                </div>
                                                <div className="flex-between mt-1">
                                                    <div className="parking-price">
                                                        ₹{parking.hourlyRate}<span>/hr</span>
                                                    </div>
                                                    <div className="rating">
                                                        ⭐ {parking.averageRating?.toFixed(1) || 'New'}
                                                    </div>
                                                </div>
                                                <div className="parking-meta">
                                                    <span className="parking-tag">{PARKING_TYPES[parking.parkingType] || 'Open'}</span>
                                                    <span className="parking-tag">{parking.availableSpots} spots</span>
                                                    {parking.is24Hours && <span className="parking-tag">24/7</span>}
                                                </div>
                                                {(() => {
                                                    const validReservations = parking.activeReservations?.filter(r => new Date(r.endDateTime) > new Date()) || [];
                                                    if (validReservations.length === 0) return null;

                                                    return (
                                                        <div style={{
                                                            marginTop: '0.75rem',
                                                            padding: '0.5rem',
                                                            background: 'rgba(239, 68, 68, 0.1)',
                                                            borderRadius: 'var(--radius-sm)',
                                                            fontSize: '0.8rem'
                                                        }}>
                                                            <strong style={{ color: '#ef4444' }}>🔒 Reserved:</strong>
                                                            <ul style={{ margin: '0.25rem 0 0 1rem', padding: 0 }}>
                                                                {validReservations.slice(0, 3).map((res, i) => (
                                                                    <li key={i} style={{ color: 'var(--color-text-muted)' }}>
                                                                        {new Date(res.startDateTime).toLocaleDateString()} {new Date(res.startDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                                                        {' → '}
                                                                        {new Date(res.endDateTime).toLocaleDateString()} {new Date(res.endDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                                                    </li>
                                                                ))}
                                                                {validReservations.length > 3 && (
                                                                    <li style={{ color: 'var(--color-text-muted)' }}>
                                                                        +{validReservations.length - 3} more reservations
                                                                    </li>
                                                                )}
                                                            </ul>
                                                        </div>
                                                    );
                                                })()}
                                            </div>
                                        </Link>
                                    ))}
                                </div>
                            </>
                        )}

                        {totalPages > 1 && (
                            <div className="flex-center gap-2 mt-2">
                                <button
                                    className="btn btn-secondary"
                                    disabled={filters.page <= 1}
                                    onClick={() => setFilters(prev => ({ ...prev, page: prev.page - 1 }))}
                                >
                                    ← Previous
                                </button>
                                <span>Page {filters.page} of {totalPages}</span>
                                <button
                                    className="btn btn-secondary"
                                    disabled={filters.page >= totalPages}
                                    onClick={() => setFilters(prev => ({ ...prev, page: prev.page + 1 }))}
                                >
                                    Next →
                                </button>
                            </div>
                        )}
                    </div>

                    {/* Right: Sticky Map */}
                    <div className="search-map" style={{ top: '80px' }}>
                        <LocationMap
                            parkingSpaces={mapParkingSpaces}
                            height="100%"
                            highlightedId={hoveredId}
                            onMarkerHover={setHoveredId}
                        />
                    </div>
                </div>
            </div>
        </div>
    );
}
