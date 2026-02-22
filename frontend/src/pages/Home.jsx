import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import toast from 'react-hot-toast';

export default function Home() {
    const [searchCity, setSearchCity] = useState('');
    const { isAuthenticated } = useAuth();
    const navigate = useNavigate();

    const handleSearch = (e) => {
        e.preventDefault();
        if (searchCity.trim()) {
            navigate(`/search?city=${encodeURIComponent(searchCity)}`);
        }
    };

    const handleNearMe = () => {
        if (!navigator.geolocation) {
            toast.error("Geolocation is not supported by your browser");
            return;
        }

        const toastId = toast.loading("Getting your location...");
        navigator.geolocation.getCurrentPosition(
            (position) => {
                toast.dismiss(toastId);
                navigate(`/search?lat=${position.coords.latitude}&lng=${position.coords.longitude}`);
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
            {/* Hero Section */}
            <section className="hero">
                <div className="container">
                    <h1 className="hero-title">
                        Find & Book<br />Parking Instantly
                    </h1>
                    <p className="hero-subtitle">
                        Discover the best parking spots near you. Book by the hour, day, week, or month.
                    </p>

                    {/* Search Box */}
                    <div className="search-box">
                        <form onSubmit={handleSearch}>
                            <div className="search-row">
                                <div className="form-group" style={{ flex: 1 }}>
                                    <label className="form-label" style={{ textAlign: 'left' }}>Location</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        placeholder="Enter city or area"
                                        value={searchCity}
                                        onChange={(e) => setSearchCity(e.target.value)}
                                    />
                                </div>
                                <div className="form-group" style={{ flex: 'none', alignSelf: 'flex-end', display: 'flex', gap: '0.5rem' }}>
                                    <button type="button" onClick={handleNearMe} className="btn btn-secondary" style={{ whiteSpace: 'nowrap' }} title="Use my current location">
                                        📍 Near Me
                                    </button>
                                    <button type="submit" className="btn btn-primary">
                                        🔍 Search
                                    </button>
                                </div>
                            </div>
                        </form>
                    </div>

                    {!isAuthenticated && (
                        <div className="mt-3 flex-center gap-2">
                            <Link to="/register" className="btn btn-outline">
                                Create Account
                            </Link>
                            <Link to="/login" className="btn btn-secondary">
                                Sign In
                            </Link>
                        </div>
                    )}
                </div>
            </section>

            {/* Features */}
            <section className="container mt-3">
                <div className="grid grid-3">
                    <div className="card">
                        <div style={{ fontSize: '2.5rem', marginBottom: '1rem' }}>🅿️</div>
                        <h3 className="card-title">Wide Selection</h3>
                        <p className="card-subtitle">
                            Browse thousands of parking spots in your area, from covered garages to open lots.
                        </p>
                    </div>
                    <div className="card">
                        <div style={{ fontSize: '2.5rem', marginBottom: '1rem' }}>⏱️</div>
                        <h3 className="card-title">Flexible Duration</h3>
                        <p className="card-subtitle">
                            Book parking by the hour, day, week, or month. Choose what works for you.
                        </p>
                    </div>
                    <div className="card">
                        <div style={{ fontSize: '2.5rem', marginBottom: '1rem' }}>💳</div>
                        <h3 className="card-title">Easy Payment</h3>
                        <p className="card-subtitle">
                            Multiple payment options available. Pay securely with cards, UPI, or wallets.
                        </p>
                    </div>
                </div>
            </section>

            {/* CTA for Vendors */}
            <section className="container mt-3 mb-3">
                <div className="card" style={{ textAlign: 'center', padding: '3rem' }}>
                    <h2 style={{ fontSize: '2rem', marginBottom: '1rem' }}>Own a Parking Space?</h2>
                    <p className="card-subtitle" style={{ maxWidth: '500px', margin: '0 auto 1.5rem' }}>
                        Start earning by renting out your unused parking space. Join our network of vendors today.
                    </p>
                    <Link to="/register" className="btn btn-primary">
                        Become a Vendor →
                    </Link>
                </div>
            </section>
        </div>
    );
}
