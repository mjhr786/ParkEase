import { useState, useEffect } from 'react';

import { useSearchParams, Link } from 'react-router-dom';
import api from '../services/api';
import INDIAN_STATES_CITIES, { STATES } from '../utils/indianStatesCities';

const PARKING_TYPES = ['Open', 'Covered', 'Garage', 'Street', 'Underground'];
const VEHICLE_TYPES = ['Car', 'Motorcycle', 'SUV', 'Truck', 'Van', 'Electric'];

export default function Search() {
    const [searchParams] = useSearchParams();
    const [parkingSpaces, setParkingSpaces] = useState([]);
    const [loading, setLoading] = useState(false);
    const [hasSearched, setHasSearched] = useState(false);
    const [filters, setFilters] = useState({
        state: '',
        city: searchParams.get('city') || '',
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

    // Only fetch when page changes AND user has already searched
    useEffect(() => {
        if (hasSearched) {
            fetchParkingSpaces();
        }
    }, [filters.page]);

    const fetchParkingSpaces = async () => {
        setLoading(true);
        try {
            const params = Object.fromEntries(
                Object.entries(filters).filter(([, v]) => v !== '')
            );
            const response = await api.searchParking(params);
            if (response.success && response.data) {
                setParkingSpaces(response.data.parkingSpaces || []);
                setTotalPages(response.data.totalPages || 1);
            }
        } catch (error) {
            console.error('Search error:', error);
        }
        setLoading(false);
    };

    const handleSearch = (e) => {
        e.preventDefault();
        setHasSearched(true);
        setFilters(prev => ({ ...prev, page: 1 }));
        fetchParkingSpaces();
    };

    const handleFilterChange = (key, value) => {
        setFilters(prev => ({ ...prev, [key]: value }));
    };

    return (
        <div className="page">
            <div className="container">
                <h1 style={{ marginBottom: '1.5rem' }}>Find Parking</h1>

                {/* Search & Filter */}
                <div className="card mb-3">
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

                            <div className="form-group" style={{ margin: 0, alignSelf: 'flex-end' }}>
                                <button type="submit" className="btn btn-primary btn-full">
                                    🔍 Search
                                </button>
                            </div>
                        </div>
                    </form>
                </div>

                {/* Results */}
                {loading ? (
                    <div className="loading">
                        <div className="spinner"></div>
                    </div>
                ) : !hasSearched ? (
                    <div className="empty-state">
                        <div className="empty-icon">🔍</div>
                        <h3>Search for Parking</h3>
                        <p>Enter a city or address and click search to find available parking spaces</p>
                    </div>
                ) : parkingSpaces.length === 0 ? (
                    <div className="empty-state">
                        <div className="empty-icon">🅿️</div>
                        <h3>No parking spaces found</h3>
                        <p>Try adjusting your search filters</p>
                    </div>
                ) : (
                    <>
                        <div className="grid grid-3">
                            {parkingSpaces.map(parking => (
                                <Link to={`/parking/${parking.id}`} key={parking.id} style={{ textDecoration: 'none', color: 'inherit' }}>
                                    <div className="card parking-card">
                                        <div className="parking-image">🅿️</div>
                                        <h3 className="card-title">{parking.title}</h3>
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
                                        {parking.activeReservations && parking.activeReservations.length > 0 && (
                                            <div style={{
                                                marginTop: '0.75rem',
                                                padding: '0.5rem',
                                                background: 'rgba(239, 68, 68, 0.1)',
                                                borderRadius: 'var(--radius-sm)',
                                                fontSize: '0.8rem'
                                            }}>
                                                <strong style={{ color: '#ef4444' }}>🔒 Reserved:</strong>
                                                <ul style={{ margin: '0.25rem 0 0 1rem', padding: 0 }}>
                                                    {parking.activeReservations.slice(0, 3).map((res, i) => (
                                                        <li key={i} style={{ color: 'var(--color-text-muted)' }}>
                                                            {new Date(res.startDateTime).toLocaleDateString()} {new Date(res.startDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                                            {' → '}
                                                            {new Date(res.endDateTime).toLocaleDateString()} {new Date(res.endDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                                        </li>
                                                    ))}
                                                    {parking.activeReservations.length > 3 && (
                                                        <li style={{ color: 'var(--color-text-muted)' }}>
                                                            +{parking.activeReservations.length - 3} more reservations
                                                        </li>
                                                    )}
                                                </ul>
                                            </div>
                                        )}
                                    </div>
                                </Link>
                            ))}
                        </div>

                        {/* Pagination */}
                        {totalPages > 1 && (
                            <div className="flex-center gap-2 mt-3">
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
                    </>
                )}
            </div>
        </div>
    );
}
