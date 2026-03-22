import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { AreaChart, Area, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { useAuth } from '../contexts/AuthContext';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';

const BOOKING_STATUS = [
    'Pending',              // 0
    'Confirmed',            // 1
    'In Progress',          // 2
    'Completed',            // 3
    'Cancelled',            // 4
    'NoShow',               // 5 (legacy)
    'Expired',              // 6 - this was the old index; backend uses 5=Expired,6=AwaitingPayment
    'Awaiting Payment',     // index 6 in backend
    'Rejected',             // 7
    'Extension Pending',    // 8
    'Extension Due',        // 9
];
// Corrected mapping aligned with backend BookingStatus enum values:
const BOOKING_STATUS_MAP = {
    0: 'Pending',
    1: 'Confirmed',
    2: 'In Progress',
    3: 'Completed',
    4: 'Cancelled',
    5: 'Expired',
    6: 'Awaiting Payment',
    7: 'Rejected',
    8: 'Extension Pending',
    9: 'Extension Due',
};
const BOOKING_STATUS_COLOR = {
    0: 'rgba(245,158,11,0.2)',  // Pending - amber
    1: 'rgba(16,185,129,0.2)', // Confirmed - green
    2: 'rgba(99,102,241,0.2)', // InProgress - indigo
    3: 'rgba(34,197,94,0.2)',  // Completed - green
    4: 'rgba(239,68,68,0.2)',  // Cancelled - red
    5: 'rgba(156,163,175,0.2)',// Expired - gray
    6: 'rgba(139,92,246,0.2)', // AwaitingPayment - purple
    7: 'rgba(239,68,68,0.2)',  // Rejected - red
    8: 'rgba(245,158,11,0.2)', // Extension Pending - amber
    9: 'rgba(139,92,246,0.2)', // Extension Due - purple
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

const REFRESH_TRIGGERS = [
    'booking.requested',
    'booking.approved',
    'booking.rejected',
    'booking.cancelled',
    'payment.completed',
    'booking.checkin',
    'booking.checkout',
    'extension.requested',
    'extension.approved',
    'extension.rejected',
];

export default function Dashboard() {
    const { user, isVendor, isMember } = useAuth();
    const [stats, setStats] = useState(null);
    const [loading, setLoading] = useState(true);

    const { subscribeToRefresh } = useNotificationContext();

    const fetchDashboard = useCallback(async () => {
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
    }, [isVendor]);

    // Initial load
    useEffect(() => {
        fetchDashboard();
    }, [fetchDashboard]);

    // Subscribe to real-time refresh events
    useEffect(() => {
        const unsubscribe = subscribeToRefresh('Dashboard', REFRESH_TRIGGERS, () => {
            // console.log('🔄 Dashboard: Auto-refreshing due to notification');
            fetchDashboard();
        });
        return unsubscribe;
    }, [subscribeToRefresh, fetchDashboard]);

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

                {/* Vendor Charts Area */}
                {isVendor && stats?.chartData && (
                    <div className="grid grid-2 mt-3 gap-2">
                        {/* Weekly Earnings Area Chart */}
                        <div className="card hover-card">
                            <h3 className="card-title mb-2">Weekly Earnings</h3>
                            <div style={{ height: '250px', width: '100%', minHeight: '0' }}>
                                <ResponsiveContainer width="100%" height="100%" minHeight={1}>
                                    <AreaChart
                                        data={stats.chartData}
                                        margin={{ top: 10, right: 10, left: 10, bottom: 0 }}
                                    >
                                        <defs>
                                            <linearGradient id="colorEarnings" x1="0" y1="0" x2="0" y2="1">
                                                <stop offset="5%" stopColor="var(--color-primary)" stopOpacity={0.8} />
                                                <stop offset="95%" stopColor="var(--color-primary)" stopOpacity={0} />
                                            </linearGradient>
                                        </defs>
                                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--color-border)" />
                                        <XAxis dataKey="label" axisLine={false} tickLine={false} stroke="var(--color-text-secondary)" fontSize={12} />
                                        <YAxis axisLine={false} tickLine={false} stroke="var(--color-text-secondary)" fontSize={12} tickFormatter={val => `₹${val}`} />
                                        <Tooltip
                                            contentStyle={{ backgroundColor: 'var(--color-surface)', borderRadius: 'var(--radius-md)', border: '1px solid var(--color-border)' }}
                                            itemStyle={{ color: 'var(--color-primary)', fontWeight: 'bold' }}
                                            formatter={(value) => [`₹${value}`, 'Earnings']}
                                        />
                                        <Area type="monotone" dataKey="earnings" stroke="var(--color-primary)" fillOpacity={1} fill="url(#colorEarnings)" />
                                    </AreaChart>
                                </ResponsiveContainer>
                            </div>
                        </div>

                        {/* Weekly Booking Volume Bar Chart */}
                        <div className="card hover-card">
                            <h3 className="card-title mb-2">Weekly Booking Volume</h3>
                            <div style={{ height: '250px', width: '100%', minHeight: '0' }}>
                                <ResponsiveContainer width="100%" height="100%" minHeight={1}>
                                    <BarChart
                                        data={stats.chartData}
                                        margin={{ top: 10, right: 10, left: 10, bottom: 0 }}
                                    >
                                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--color-border)" />
                                        <XAxis dataKey="label" axisLine={false} tickLine={false} stroke="var(--color-text-secondary)" fontSize={12} />
                                        <YAxis axisLine={false} tickLine={false} stroke="var(--color-text-secondary)" fontSize={12} allowDecimals={false} />
                                        <Tooltip
                                            contentStyle={{ backgroundColor: 'var(--color-surface)', borderRadius: 'var(--radius-md)', border: '1px solid var(--color-border)' }}
                                            itemStyle={{ color: 'var(--color-secondary)', fontWeight: 'bold' }}
                                            formatter={(value) => [value, 'Bookings']}
                                        />
                                        <Bar dataKey="volume" fill="var(--color-secondary)" radius={[4, 4, 0, 0]} barSize={30} />
                                    </BarChart>
                                </ResponsiveContainer>
                            </div>
                        </div>
                    </div>
                )}

                {/* Upcoming/Recent Bookings */}
                <div className="card hover-card mt-3">
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
                                            background: BOOKING_STATUS_COLOR[booking.status] ??
                                                'rgba(99,102,241,0.2)'
                                        }}>
                                            {BOOKING_STATUS_MAP[booking.status] ?? 'Unknown'}
                                        </div>
                                        <div style={{ marginTop: '0.5rem', fontWeight: 600 }}>
                                            ₹{booking.totalAmount}
                                        </div>
                                        {booking.status === 2 && ( // InProgress
                                            <div className="mt-1" style={{ display: 'flex', justifyContent: 'flex-end' }}>
                                                <CountdownTimer endDateTime={booking.endDateTime} />
                                            </div>
                                        )}
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
