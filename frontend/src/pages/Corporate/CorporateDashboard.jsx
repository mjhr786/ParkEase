import React, { useEffect, useState } from 'react';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';
import { useThemeColors } from '../../hooks/useThemeColors';

const formatMoney = (value) => {
    const n = Number(value);
    if (Number.isNaN(n)) return '—';
    return n.toLocaleString(undefined, { style: 'currency', currency: 'INR', maximumFractionDigits: 0 });
};

const CorporateDashboard = () => {
    const { activeCompanyId, companyDetails, isCorporateMode } = useCompany();
    const [stats, setStats] = useState(null);
    const [loading, setLoading] = useState(true);
    const [exporting, setExporting] = useState(false);
    const navigate = useNavigate();
    const colors = useThemeColors();

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

    const handleExport = async () => {
        setExporting(true);
        try {
            const { blob, fileName } = await corporateService.exportDashboard();
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName || `corporate-dashboard-${new Date().toISOString().slice(0, 10)}.csv`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
            toast.success('Dashboard CSV downloaded');
        } catch (err) {
            toast.error(err?.message || 'Export failed');
        } finally {
            setExporting(false);
        }
    };

    if (!isCorporateMode) return null;

    if (loading) {
        return <div className="loading" style={{ minHeight: '60vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}><div className="spinner"></div></div>;
    }

    if (!stats) {
        return <div className="container" style={{ padding: '2rem 0', color: 'var(--color-text-primary)' }}>No data available</div>;
    }

    const panelStyle = {
        background: 'var(--color-surface)',
        borderRadius: '12px',
        padding: '1.5rem',
        border: '1px solid var(--color-border)',
    };

    return (
        <div className="container" style={{ padding: '2rem 0', color: 'var(--color-text)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', marginBottom: '1.5rem', flexWrap: 'wrap' }}>
                <div>
                    <h1 style={{ marginBottom: '0.5rem', color: 'var(--color-text-primary)', display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <span style={{ fontSize: '2rem' }}>🏢</span> {companyDetails?.name || 'Company'} Dashboard
                    </h1>
                    <p style={{ color: 'var(--color-text-secondary)', margin: 0 }}>
                        Overview of corporate parking usage and policies for {companyDetails?.name}. Export is a full snapshot for reporting (not invoicing).
                    </p>
                </div>
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                    <button type="button" className="btn btn-secondary" onClick={() => navigate('/corporate/bookings')}>
                        View bookings
                    </button>
                    <button type="button" className="btn btn-primary" onClick={handleExport} disabled={exporting}>
                        {exporting ? 'Exporting…' : 'Export CSV'}
                    </button>
                </div>
            </div>

            {/* Top Stat Cards */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.5rem', marginBottom: '2rem' }}>
                <StatCard title="Active Members" value={stats.activeMembers} secondary={`out of ${stats.totalMembers} total`} icon="👨‍💼" />
                <StatCard title="Active Allocations" value={stats.activeAllocations} secondary={`out of ${stats.totalAllocations} total spaces`} icon="🅿️" />
                <StatCard title="Owned Parking" value={stats.ownedParkingSpaces || 0} secondary={`${stats.ownedParkingSlots || 0} owned slots`} icon="🏗️" color="#38bdf8" />
                <StatCard title="Leased Parking" value={stats.leasedAllocations || 0} secondary={`${stats.pendingVendorAllocations || 0} pending approvals`} icon="🤝" color="var(--color-secondary)" />
                <StatCard title="Waitlist Pressure" value={stats.activeWaitlistEntries || 0} secondary="Pending waitlist entries" icon="⏳" color="var(--color-warning)" />
                <StatCard title="Expiring (30d)" value={stats.expiringAllocationsWithin30Days || 0} secondary="Active contracts ending soon" icon="📆" color="#f97316" />
                <StatCard title="Bookings This Month" value={stats.totalBookingsThisMonth} secondary={`including ${stats.visitorBookingsThisMonth} visitors`} icon="📅" />
                <StatCard title="Hours Used (Month)" value={Number(stats.totalHoursUsedThisMonth || 0).toFixed(1)} secondary="Employee + visitor hours" icon="⏱️" color="#22d3ee" />
                <StatCard title="Monthly Spend" value={formatMoney(stats.monthlySpend)} secondary="Active allocation rates · see Invoices for settlement" icon="💰" color="var(--color-success)" />
                <StatCard title="Utilization Rate" value={`${stats.utilizationPercentage}%`} secondary={`Today's slot usage`} icon="📊" />
                <StatCard title="Suspicious Activity" value={stats.suspiciousActivityCount} secondary={`Overlapping bookings detected`} icon="⚠️" color="var(--color-error)" />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '2rem' }}>
                {/* 7-Day Trend Chart */}
                <div style={panelStyle}>
                    <h3 style={{ marginBottom: '1.5rem', color: 'var(--color-text-primary)', fontSize: '1.1rem' }}>Booking Trend (Last 7 Days)</h3>
                    <div style={{ height: '300px' }}>
                        <ResponsiveContainer width="100%" height="100%">
                            <LineChart data={stats.bookingsByDay}>
                                <CartesianGrid strokeDasharray="3 3" stroke={colors.border} />
                                <XAxis dataKey="label" stroke={colors.textSecondary} />
                                <YAxis stroke={colors.textSecondary} allowDecimals={false} />
                                <Tooltip contentStyle={{ backgroundColor: colors.bgPrimary, border: `1px solid ${colors.border}`, borderRadius: '8px', color: colors.textPrimary }} />
                                <Line type="monotone" dataKey="volume" stroke={colors.secondary} strokeWidth={3} dot={{ r: 4, fill: colors.secondary }} activeDot={{ r: 6 }} name="Bookings" />
                            </LineChart>
                        </ResponsiveContainer>
                    </div>
                </div>

                {/* Allocation Breakdown */}
                <div style={panelStyle}>
                    <h3 style={{ marginBottom: '1.5rem', color: 'var(--color-text-primary)', fontSize: '1.1rem' }}>Space Utilization Today</h3>
                    <div style={{ height: '300px' }}>
                        <ResponsiveContainer width="100%" height="100%">
                            <BarChart data={stats.allocationBreakdown} layout="vertical" margin={{ left: 50 }}>
                                <CartesianGrid strokeDasharray="3 3" stroke={colors.border} horizontal={false} />
                                <XAxis type="number" stroke={colors.textSecondary} domain={[0, 100]} tickFormatter={(val) => `${val}%`} />
                                <YAxis type="category" dataKey="parkingSpaceTitle" stroke={colors.textSecondary} width={100} />
                                <Tooltip
                                    contentStyle={{ backgroundColor: colors.bgPrimary, border: `1px solid ${colors.border}`, borderRadius: '8px', color: colors.textPrimary }}
                                    formatter={(value, name) => {
                                        if (name === 'utilizationPercent') return [`${value}%`, 'Utilization'];
                                        return [value, name];
                                    }}
                                />
                                <Bar dataKey="utilizationPercent" fill={colors.success} radius={[0, 4, 4, 0]} name="Utilization %" />
                            </BarChart>
                        </ResponsiveContainer>
                    </div>
                </div>
            </div>

            {/* Expiring contracts */}
            {(stats.expiringAllocations?.length > 0 || (stats.expiringAllocationsWithin30Days || 0) > 0) && (
                <div style={{ ...panelStyle, border: '1px solid rgba(249,115,22,0.25)', marginTop: '2rem' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', gap: '1rem', flexWrap: 'wrap' }}>
                        <h3 style={{ margin: 0, color: 'var(--color-text-primary)', fontSize: '1.1rem' }}>Contracts expiring within 30 days</h3>
                        <button type="button" className="btn btn-secondary" onClick={() => navigate('/corporate/allocations')}>
                            Manage allocations
                        </button>
                    </div>
                    {stats.expiringAllocations?.length > 0 ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.65rem' }}>
                            {stats.expiringAllocations.map((item) => (
                                <div
                                    key={item.allocationId}
                                    style={{
                                        display: 'grid',
                                        gridTemplateColumns: '1.4fr 1fr 1fr',
                                        gap: '0.75rem',
                                        padding: '0.85rem 1rem',
                                        background: 'rgba(249,115,22,0.08)',
                                        borderRadius: '8px',
                                        border: '1px solid rgba(249,115,22,0.2)',
                                    }}
                                >
                                    <div>
                                        <strong style={{ color: 'var(--color-text-primary)' }}>{item.parkingSpaceTitle}</strong>
                                        <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.8rem' }}>
                                            {item.sourceType === 1 ? 'Owned' : 'Leased'}
                                            {item.leaseReference ? ` · ${item.leaseReference}` : ''}
                                        </div>
                                    </div>
                                    <div style={{ color: 'var(--color-warning)', fontSize: '0.9rem' }}>
                                        Ends {new Date(item.endDate).toLocaleDateString()}
                                    </div>
                                    <div style={{ color: 'var(--color-text)', fontSize: '0.9rem', textAlign: 'right' }}>
                                        ₹{Number(item.monthlyRate || 0).toLocaleString()}
                                        /mo
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <p style={{ color: 'var(--color-text-secondary)', margin: 0 }}>Expiring contracts detected — open Allocations to review terms.</p>
                    )}
                </div>
            )}

            {/* Peak Hours & Fraud */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '2rem', marginTop: '2rem' }}>
                <div style={panelStyle}>
                    <h3 style={{ marginBottom: '1.5rem', color: 'var(--color-text-primary)', fontSize: '1.1rem' }}>Top Peak Hours</h3>
                    {stats.peakHours?.length > 0 ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                            {stats.peakHours.map((ph, idx) => (
                                <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'var(--color-surface-elevated)', borderRadius: '8px' }}>
                                    <span>{ph.hourOfDay}:00 - {ph.hourOfDay + 1}:00</span>
                                    <strong style={{ color: 'var(--color-secondary)' }}>{ph.bookingCount} bookings</strong>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <p style={{ color: 'var(--color-text-secondary)' }}>No peak hour data available for this month.</p>
                    )}
                </div>

                <div style={panelStyle}>
                    <h3 style={{ marginBottom: '1.5rem', color: 'var(--color-error)', fontSize: '1.1rem', display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span>🚨</span> Fraud Alerts
                    </h3>
                    {stats.fraudAlerts?.length > 0 ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                            {stats.fraudAlerts.map((fa, idx) => (
                                <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', padding: '12px', background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.2)', borderRadius: '8px' }}>
                                    <div style={{ display: 'flex', flexDirection: 'column' }}>
                                        <strong style={{ color: 'var(--color-text-primary)' }}>{fa.userName}</strong>
                                        <span style={{ fontSize: '0.8rem', color: 'var(--color-error)' }}>{fa.overlappingBookingPairs} overlapping active bookings</span>
                                    </div>
                                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end' }}>
                                        <strong style={{ color: 'var(--color-error)' }}>Risk {fa.riskScore}</strong>
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <p style={{ color: 'var(--color-success)' }}>No suspicious overlapping bookings detected currently.</p>
                    )}
                </div>
            </div>
        </div>
    );
};

const StatCard = ({ title, value, secondary, icon, color = 'var(--color-secondary)' }) => (
    <div style={{ background: 'var(--color-surface)', padding: '1.5rem', borderRadius: '12px', border: '1px solid var(--color-border)' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem' }}>
            <span style={{ fontSize: '0.9rem', color: 'var(--color-text-secondary)', fontWeight: '500' }}>{title}</span>
            <span style={{ fontSize: '1.5rem' }}>{icon}</span>
        </div>
        <div style={{ fontSize: '2rem', fontWeight: '700', color: 'var(--color-text-primary)', marginBottom: '4px' }}>{value}</div>
        <div style={{ fontSize: '0.8rem', color: color }}>{secondary}</div>
    </div>
);

export default CorporateDashboard;
