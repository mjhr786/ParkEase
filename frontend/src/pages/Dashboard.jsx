import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import api from '../services/api';

const BOOKING_STATUS = ['Pending', 'Confirmed', 'InProgress', 'Completed', 'Cancelled', 'NoShow', 'Expired'];

export default function Dashboard() {
    const { user, isVendor, isMember } = useAuth();
    const [stats, setStats] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        fetchDashboard();
    }, []);

    const fetchDashboard = async () => {
        try {
            const response = isVendor
                ? await api.getVendorDashboard()
                : await api.getMemberDashboard();

            if (response.success && response.data) {
                setStats(response.data);
            }
        } catch (error) {
            console.error('Dashboard error:', error);
        }
        setLoading(false);
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

    return (
        <div className="page">
            <div className="container">
                <div className="dashboard-header flex-between">
                    <div>
                        <h1 className="dashboard-title">Welcome, {user?.firstName}!</h1>
                        <p className="card-subtitle">{isVendor ? 'Vendor Dashboard' : 'Member Dashboard'}</p>
                    </div>
                    {isVendor && (
                        <Link to="/vendor/listings" className="btn btn-primary">
                            + Add Parking Space
                        </Link>
                    )}
                </div>

                {/* Stats Grid */}
                <div className="grid grid-4 mt-3">
                    {isVendor ? (
                        <>
                            <div className="card stat-card">
                                <div className="stat-value">{stats?.totalParkingSpaces || 0}</div>
                                <div className="stat-label">Total Listings</div>
                            </div>
                            <div className="card stat-card">
                                <div className="stat-value">{stats?.activeBookings || 0}</div>
                                <div className="stat-label">Active Bookings</div>
                            </div>
                            <div className="card stat-card">
                                <div className="stat-value">₹{stats?.monthlyEarnings?.toFixed(0) || 0}</div>
                                <div className="stat-label">Monthly Earnings</div>
                            </div>
                            <div className="card stat-card">
                                <div className="stat-value">⭐ {stats?.averageRating?.toFixed(1) || 'N/A'}</div>
                                <div className="stat-label">Average Rating</div>
                            </div>
                        </>
                    ) : (
                        <>
                            <div className="card stat-card">
                                <div className="stat-value">{stats?.totalBookings || 0}</div>
                                <div className="stat-label">Total Bookings</div>
                            </div>
                            <div className="card stat-card">
                                <div className="stat-value">{stats?.activeBookings || 0}</div>
                                <div className="stat-label">Active Bookings</div>
                            </div>
                            <div className="card stat-card">
                                <div className="stat-value">{stats?.completedBookings || 0}</div>
                                <div className="stat-label">Completed</div>
                            </div>
                            <div className="card stat-card">
                                <div className="stat-value">₹{stats?.totalSpent?.toFixed(0) || 0}</div>
                                <div className="stat-label">Total Spent</div>
                            </div>
                        </>
                    )}
                </div>

                {/* Vendor Earnings Chart */}
                {isVendor && stats?.earningsChart && (
                    <div className="card mt-3">
                        <h3 className="card-title">Weekly Earnings</h3>
                        <div className="flex gap-1 mt-2" style={{ alignItems: 'flex-end', height: '200px' }}>
                            {stats.earningsChart.map((day, i) => (
                                <div key={i} style={{ flex: 1, textAlign: 'center' }}>
                                    <div
                                        style={{
                                            height: `${Math.max(10, (day.amount / Math.max(...stats.earningsChart.map(d => d.amount || 1))) * 150)}px`,
                                            background: 'var(--gradient-primary)',
                                            borderRadius: 'var(--radius-sm)',
                                            marginBottom: '0.5rem',
                                        }}
                                    ></div>
                                    <small style={{ color: 'var(--color-text-secondary)' }}>{day.day}</small>
                                </div>
                            ))}
                        </div>
                    </div>
                )}

                {/* Upcoming/Recent Bookings */}
                <div className="card mt-3">
                    <h3 className="card-title mb-2">
                        {isMember ? 'Upcoming Bookings' : 'Recent Bookings'}
                    </h3>

                    {(!stats?.upcomingBookings?.length && !stats?.recentBookings?.length) ? (
                        <p className="card-subtitle">No bookings to display</p>
                    ) : (
                        <div>
                            {(isMember ? stats?.upcomingBookings : stats?.recentBookings)?.map(booking => (
                                <div
                                    key={booking.id}
                                    className="flex-between"
                                    style={{
                                        padding: '1rem',
                                        borderBottom: '1px solid var(--color-border)',
                                    }}
                                >
                                    <div>
                                        <strong>{booking.parkingSpaceTitle}</strong>
                                        <div className="card-subtitle">
                                            {new Date(booking.startDateTime).toLocaleDateString()} -{' '}
                                            {new Date(booking.endDateTime).toLocaleDateString()}
                                        </div>
                                        <small>Ref: {booking.bookingReference}</small>
                                    </div>
                                    <div style={{ textAlign: 'right' }}>
                                        <div className="parking-tag" style={{
                                            background: booking.status === 1 ? 'rgba(16, 185, 129, 0.2)' :
                                                booking.status === 4 ? 'rgba(239, 68, 68, 0.2)' :
                                                    'rgba(99, 102, 241, 0.2)'
                                        }}>
                                            {BOOKING_STATUS[booking.status]}
                                        </div>
                                        <div style={{ marginTop: '0.5rem', fontWeight: 600 }}>
                                            ₹{booking.totalAmount}
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    <Link to="/bookings" className="btn btn-secondary mt-2">
                        View All Bookings →
                    </Link>
                </div>
            </div>
        </div>
    );
}
