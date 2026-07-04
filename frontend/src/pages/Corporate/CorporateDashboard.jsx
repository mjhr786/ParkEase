import React, { useEffect, useState } from 'react';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';

const CorporateDashboard = () => {
    const { activeCompanyId, companyDetails, isCorporateMode } = useCompany();
    const [stats, setStats] = useState(null);
    const [loading, setLoading] = useState(true);
    const navigate = useNavigate();

    useEffect(() => {
        if (!isCorporateMode) {
            navigate('/dashboard', { replace: true });
            return;
        }

        const loadStats = async () => {
            setLoading(true);
            try {
                const response = await corporateService.getDashboard();
                if (response.success && response.data) {
                    setStats(response.data);
                } else {
                    toast.error(response.message || "Failed to load dashboard statistics");
                }
            } catch (error) {
                toast.error("Could not reach server");
            } finally {
                setLoading(false);
            }
        };

        loadStats();
    }, [activeCompanyId, isCorporateMode, navigate]);

    if (!isCorporateMode) return null;

    if (loading) {
        return <div className="loading" style={{ minHeight: '60vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}><div className="spinner"></div></div>;
    }

    if (!stats) {
        return <div className="container" style={{ padding: '2rem 0', color: 'white' }}>No data available</div>;
    }

    return (
        <div className="container" style={{ padding: '2rem 0', color: '#f1f5f9' }}>
            <h1 style={{ marginBottom: '0.5rem', color: 'white', display: 'flex', alignItems: 'center', gap: '10px' }}>
                <span style={{ fontSize: '2rem' }}>🏢</span> {companyDetails?.name || 'Company'} Dashboard
            </h1>
            <p style={{ color: '#94a3b8', marginBottom: '2rem' }}>Overview of corporate parking usage and policies for {companyDetails?.name}.</p>

            {/* Top Stat Cards */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.5rem', marginBottom: '2rem' }}>
                <StatCard title="Active Members" value={stats.activeMembers} secondary={`out of ${stats.totalMembers} total`} icon="👨‍💼" />
                <StatCard title="Active Allocations" value={stats.activeAllocations} secondary={`out of ${stats.totalAllocations} total spaces`} icon="🅿️" />
                <StatCard title="Owned Parking" value={stats.ownedParkingSpaces || 0} secondary={`${stats.ownedParkingSlots || 0} owned slots`} icon="🏗️" color="#38bdf8" />
                <StatCard title="Leased Parking" value={stats.leasedAllocations || 0} secondary={`${stats.pendingVendorAllocations || 0} pending approvals`} icon="🤝" color="#a78bfa" />
                <StatCard title="Bookings This Month" value={stats.totalBookingsThisMonth} secondary={`including ${stats.visitorBookingsThisMonth} visitors`} icon="📅" />
                <StatCard title="Utilization Rate" value={`${stats.utilizationPercentage}%`} secondary={`Today's slot usage`} icon="📊" />
                <StatCard title="Suspicious Activity" value={stats.suspiciousActivityCount} secondary={`Overlapping bookings detected`} icon="⚠️" color="#ef4444" />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '2rem' }}>
                {/* 7-Day Trend Chart */}
                <div style={{ background: '#1e293b', borderRadius: '12px', padding: '1.5rem', border: '1px solid rgba(255,255,255,0.05)' }}>
                    <h3 style={{ marginBottom: '1.5rem', color: 'white', fontSize: '1.1rem' }}>Booking Trend (Last 7 Days)</h3>
                    <div style={{ height: '300px' }}>
                        <ResponsiveContainer width="100%" height="100%">
                            <LineChart data={stats.bookingsByDay}>
                                <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                                <XAxis dataKey="day" stroke="#94a3b8" />
                                <YAxis stroke="#94a3b8" allowDecimals={false} />
                                <Tooltip contentStyle={{ backgroundColor: '#0f172a', border: '1px solid #334155', borderRadius: '8px' }} />
                                <Line type="monotone" dataKey="bookingCount" stroke="#8b5cf6" strokeWidth={3} dot={{ r: 4, fill: '#8b5cf6' }} activeDot={{ r: 6 }} name="Bookings" />
                            </LineChart>
                        </ResponsiveContainer>
                    </div>
                </div>

                {/* Allocation Breakdown */}
                <div style={{ background: '#1e293b', borderRadius: '12px', padding: '1.5rem', border: '1px solid rgba(255,255,255,0.05)' }}>
                    <h3 style={{ marginBottom: '1.5rem', color: 'white', fontSize: '1.1rem' }}>Space Utilization Today</h3>
                    <div style={{ height: '300px' }}>
                        <ResponsiveContainer width="100%" height="100%">
                            <BarChart data={stats.allocationBreakdown} layout="vertical" margin={{ left: 50 }}>
                                <CartesianGrid strokeDasharray="3 3" stroke="#334155" horizontal={false} />
                                <XAxis type="number" stroke="#94a3b8" domain={[0, 100]} tickFormatter={(val) => `${val}%`} />
                                <YAxis type="category" dataKey="parkingSpaceTitle" stroke="#94a3b8" width={100} />
                                <Tooltip 
                                    contentStyle={{ backgroundColor: '#0f172a', border: '1px solid #334155', borderRadius: '8px' }} 
                                    formatter={(value, name, props) => {
                                        if (name === 'utilizationPercent') return [`${value}%`, 'Utilization'];
                                        return [value, name];
                                    }}
                                />
                                <Bar dataKey="utilizationPercent" fill="#10b981" radius={[0, 4, 4, 0]} name="Utilization %" />
                            </BarChart>
                        </ResponsiveContainer>
                    </div>
                </div>
            </div>

            {/* Peak Hours & Fraud */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '2rem', marginTop: '2rem' }}>
                <div style={{ background: '#1e293b', borderRadius: '12px', padding: '1.5rem', border: '1px solid rgba(255,255,255,0.05)' }}>
                    <h3 style={{ marginBottom: '1.5rem', color: 'white', fontSize: '1.1rem' }}>Top Peak Hours</h3>
                    {stats.peakHours?.length > 0 ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                            {stats.peakHours.map((ph, idx) => (
                                <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'rgba(255,255,255,0.03)', borderRadius: '8px' }}>
                                    <span>{ph.hourOfDay}:00 - {ph.hourOfDay + 1}:00</span>
                                    <strong style={{ color: '#8b5cf6' }}>{ph.bookingCount} bookings</strong>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <p style={{ color: '#94a3b8' }}>No peak hour data available for this month.</p>
                    )}
                </div>

                <div style={{ background: '#1e293b', borderRadius: '12px', padding: '1.5rem', border: '1px solid rgba(255,255,255,0.05)' }}>
                    <h3 style={{ marginBottom: '1.5rem', color: '#ef4444', fontSize: '1.1rem', display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span>🚨</span> Fraud Alerts
                    </h3>
                    {stats.fraudAlerts?.length > 0 ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                            {stats.fraudAlerts.map((fa, idx) => (
                                <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.2)', borderRadius: '8px' }}>
                                    <div style={{ display: 'flex', flexDirection: 'column' }}>
                                        <strong style={{ color: 'white' }}>{fa.userName}</strong>
                                        <span style={{ fontSize: '0.8rem', color: '#f87171' }}>{fa.overlappingBookingPairs} overlapping active bookings</span>
                                    </div>
                                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end' }}>
                                        <strong style={{ color: '#ef4444' }}>Risk {fa.riskScore}</strong>
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <p style={{ color: '#10b981' }}>No suspicious overlapping bookings detected currently.</p>
                    )}
                </div>
            </div>
        </div>
    );
};

const StatCard = ({ title, value, secondary, icon, color = "#8b5cf6" }) => (
    <div style={{ background: '#1e293b', padding: '1.5rem', borderRadius: '12px', border: '1px solid rgba(255,255,255,0.05)' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem' }}>
            <span style={{ fontSize: '0.9rem', color: '#94a3b8', fontWeight: '500' }}>{title}</span>
            <span style={{ fontSize: '1.5rem' }}>{icon}</span>
        </div>
        <div style={{ fontSize: '2rem', fontWeight: '700', color: 'white', marginBottom: '4px' }}>{value}</div>
        <div style={{ fontSize: '0.8rem', color: color }}>{secondary}</div>
    </div>
);

export default CorporateDashboard;
