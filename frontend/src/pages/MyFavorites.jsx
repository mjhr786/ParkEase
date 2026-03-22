import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import api from '../services/api';
import showToast from '../utils/toast.jsx';
import { API_BASE_URL } from '../config';

export default function MyFavorites() {
    const { isAuthenticated } = useAuth();
    const navigate = useNavigate();

    const [favorites, setFavorites] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!isAuthenticated) {
            navigate('/login');
            return;
        }
        fetchFavorites();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isAuthenticated, navigate]);

    const fetchFavorites = async () => {
        try {
            const res = await api.getMyFavorites();
            if (res.success && res.data) {
                setFavorites(res.data);
            }
        } catch (err) {
            showToast.error('Failed to load favorites');
        } finally {
            setLoading(false);
        }
    };

    const removeFavorite = async (e, id) => {
        e.preventDefault(); // Prevent navigating to details
        try {
            const res = await api.toggleFavorite(id);
            if (res.success) {
                setFavorites(prev => prev.filter(f => f.id !== id));
                showToast.success('Removed from favorites');
            }
        } catch (err) {
            showToast.error('Failed to remove favorite');
        }
    };

    if (!isAuthenticated) return null;

    if (loading) {
        return (
            <div className="page">
                <div className="container">
                    <h1 style={{ marginBottom: '2rem' }}>My Favorites</h1>
                    <div className="grid grid-3">
                        {[1, 2, 3, 4, 5, 6].map(n => (
                            <div key={n} className="skeleton-card" />
                        ))}
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="page">
            <div className="container">
                <h1 style={{ marginBottom: '2rem' }}>My Favorites</h1>

                {favorites.length === 0 ? (
                    <div className="empty-state">
                        <div className="empty-icon">❤️</div>
                        <h3>No Saved Spaces Yet</h3>
                        <p>You haven't added any parking spaces to your favorites.</p>
                        <button className="btn btn-primary mt-2" onClick={() => navigate('/search')}>
                            🔍 Find Parking
                        </button>
                    </div>
                ) : (
                    <div className="grid grid-3">
                        {favorites.map(parking => (
                            <Link
                                to={`/parking/${parking.id}`}
                                key={parking.id}
                                style={{ textDecoration: 'none', color: 'inherit' }}
                                className="card parking-card hover-card"
                            >
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
                                    onClick={(e) => removeFavorite(e, parking.id)}
                                    title="Unsave"
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
                                    ❤️
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
                            </Link>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
