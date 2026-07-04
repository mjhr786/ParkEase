import React, { useEffect, useState } from 'react';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';

const formatPolicyTime = (value, fallback) => {
    if (!value) return fallback;
    return String(value).slice(0, 5);
};

const toTimeSpan = (value) => {
    if (!value) return null;
    return value.length === 5 ? `${value}:00` : value;
};

const badgeStyles = {
    owned: { background: 'rgba(167, 139, 250, 0.14)', color: '#c4b5fd', border: '1px solid rgba(167, 139, 250, 0.35)' },
    leased: { background: 'rgba(56, 189, 248, 0.12)', color: '#7dd3fc', border: '1px solid rgba(56, 189, 248, 0.32)' },
    pending: { background: 'rgba(245, 158, 11, 0.12)', color: '#fbbf24', border: '1px solid rgba(245, 158, 11, 0.32)' },
    active: { background: 'rgba(16, 185, 129, 0.12)', color: '#6ee7b7', border: '1px solid rgba(16, 185, 129, 0.32)' },
    expired: { background: 'rgba(148, 163, 184, 0.12)', color: '#cbd5e1', border: '1px solid rgba(148, 163, 184, 0.28)' },
    rejected: { background: 'rgba(239, 68, 68, 0.12)', color: '#fca5a5', border: '1px solid rgba(239, 68, 68, 0.3)' }
};

const StatusBadge = ({ children, tone }) => (
    <span style={{
        ...badgeStyles[tone],
        display: 'inline-flex',
        alignItems: 'center',
        minHeight: '26px',
        padding: '3px 10px',
        borderRadius: '999px',
        fontSize: '0.78rem',
        fontWeight: 700,
        whiteSpace: 'nowrap'
    }}>
        {children}
    </span>
);

const getStatusBadge = (status) => {
    if (status === 0) return <StatusBadge tone="pending">Pending Vendor Approval</StatusBadge>;
    if (status === 1) return <StatusBadge tone="active">Active</StatusBadge>;
    if (status === 2) return <StatusBadge tone="rejected">Rejected</StatusBadge>;
    return <StatusBadge tone="expired">Expired</StatusBadge>;
};

const CompanyAllocations = () => {
    const { activeCompanyId, isCorporateMode } = useCompany();
    const navigate = useNavigate();

    const [allocations, setAllocations] = useState([]);
    const [waitlist, setWaitlist] = useState([]);
    const [loading, setLoading] = useState(true);
    const [sourceModalOpen, setSourceModalOpen] = useState(false);

    // Modal state for Policy Update
    const [policyModalObj, setPolicyModalObj] = useState(null);
    const [updatingPolicy, setUpdatingPolicy] = useState(false);

    // Modal state for Fixed Slot Assignment
    const [fixedSlotModalObj, setFixedSlotModalObj] = useState(null);
    const [assigningSlot, setAssigningSlot] = useState(false);
    const [members, setMembers] = useState([]);

    useEffect(() => {
        if (!isCorporateMode) {
            navigate('/dashboard', { replace: true });
            return;
        }
        loadAllocations();
    }, [activeCompanyId, isCorporateMode, navigate]);

    const loadAllocations = async () => {
        setLoading(true);
        try {
            const response = await corporateService.getAllocations();
            if (response.success && response.data) {
                setAllocations(response.data);
            } else {
                toast.error(response.message || "Failed to load allocations");
            }
            // Fetch members silently
            const memRes = await corporateService.getMembers(1, 100);
            if (memRes.success && memRes.data) {
                setMembers(memRes.data.members?.filter(m => m.isActive) || []);
            }
            const waitlistRes = await corporateService.getWaitlist();
            if (waitlistRes.success && waitlistRes.data) {
                setWaitlist(waitlistRes.data);
            } else {
                setWaitlist([]);
            }
        } catch (error) {
            toast.error("Could not reach server");
        } finally {
            setLoading(false);
        }
    };

    const handleUpdatePolicy = async (e) => {
        e.preventDefault();
        const allowedStartTime = formatPolicyTime(policyModalObj.policy?.allowedStartTime, '07:00');
        const allowedEndTime = formatPolicyTime(policyModalObj.policy?.allowedEndTime, '22:00');
        if (allowedEndTime <= allowedStartTime) {
            toast.error('Allowed end time must be after allowed start time.');
            return;
        }
        setUpdatingPolicy(true);
        try {
            const payload = {
                maxBookingsPerEmployeePerDay: parseInt(policyModalObj.policy.maxBookingsPerEmployeePerDay),
                maxBookingsPerEmployeePerWeek: parseInt(policyModalObj.policy.maxBookingsPerEmployeePerWeek),
                priorityThreshold: parseInt(policyModalObj.policy.priorityThreshold),
                allowedStartTime: toTimeSpan(allowedStartTime),
                allowedEndTime: toTimeSpan(allowedEndTime),
                allowWeekends: policyModalObj.policy.allowWeekends
            };
            const response = await corporateService.updatePolicy(policyModalObj.id, payload);
            
            if (response.success) {
                toast.success('Policy updated successfully!');
                setPolicyModalObj(null);
                loadAllocations();
            } else {
                toast.error(response.message || 'Failed to update policy');
            }
        } catch (error) {
            toast.error('An error occurred during policy update');
        } finally {
            setUpdatingPolicy(false);
        }
    };

    const handleAssignFixedSlot = async (e) => {
        e.preventDefault();
        setAssigningSlot(true);
        try {
            const payload = {
                membershipId: fixedSlotModalObj.membershipId,
                slotNumber: parseInt(fixedSlotModalObj.slotNumber)
            };
            const response = await corporateService.assignFixedSlot(fixedSlotModalObj.allocationId, payload);
            
            if (response.success) {
                toast.success('Fixed slot assigned successfully!');
                setFixedSlotModalObj(null);
                loadAllocations();
            } else {
                toast.error(response.message || 'Failed to assign fixed slot');
            }
        } catch (error) {
            toast.error('An error occurred during slot assignment');
        } finally {
            setAssigningSlot(false);
        }
    };

    const handleRemoveFixedSlot = async (allocationId, membershipId) => {
        if (!window.confirm('Remove this fixed slot assignment?')) return;

        try {
            const response = await corporateService.removeFixedSlot(allocationId, membershipId);
            if (response.success) {
                toast.success('Fixed slot assignment removed.');
                loadAllocations();
            } else {
                toast.error(response.message || 'Failed to remove fixed slot');
            }
        } catch (error) {
            toast.error('An error occurred while removing the fixed slot');
        }
    };

    const handleCancelWaitlist = async (waitlistEntryId) => {
        if (!window.confirm('Cancel this waitlist entry?')) return;

        try {
            const response = await corporateService.cancelWaitlistEntry(waitlistEntryId);
            if (response.success) {
                toast.success('Waitlist entry cancelled.');
                loadAllocations();
            } else {
                toast.error(response.message || 'Failed to cancel waitlist entry');
            }
        } catch (error) {
            toast.error('An error occurred while cancelling the waitlist entry');
        }
    };

    const handlePromoteWaitlist = async (waitlistEntryId) => {
        try {
            const response = await corporateService.promoteWaitlistEntry(waitlistEntryId);
            if (response.success) {
                toast.success('Waitlist entry promoted to a confirmed booking.');
                loadAllocations();
            } else {
                toast.error(response.message || 'Failed to promote waitlist entry');
            }
        } catch (error) {
            toast.error('An error occurred while promoting the waitlist entry');
        }
    };

    if (!isCorporateMode) return null;

    return (
        <div className="container" style={{ padding: '2rem 0', color: '#f1f5f9' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
                <h1 style={{ color: 'white', display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontSize: '2rem' }}>🅿️</span> Parking Allocations
                </h1>
                <button
                    onClick={() => setSourceModalOpen(true)}
                    className="btn btn-primary"
                    style={{ padding: '0.6rem 1.2rem' }}
                >
                    Request New Allocation
                </button>
            </div>

            {waitlist.length > 0 && (
                <div style={{ background: '#1e293b', borderRadius: '12px', padding: '1.5rem', marginBottom: '1.5rem', border: '1px solid rgba(255,255,255,0.05)' }}>
                    <h2 style={{ margin: '0 0 1rem 0', color: 'white', fontSize: '1.15rem' }}>Waitlist</h2>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                        {waitlist.map(entry => (
                            <div key={entry.id} style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr 1fr auto', gap: '1rem', alignItems: 'center', background: 'rgba(255,255,255,0.02)', padding: '0.85rem', borderRadius: '8px' }}>
                                <div>
                                    <div style={{ color: '#e2e8f0', fontWeight: 600 }}>{entry.isVisitorBooking ? entry.visitorName : entry.vehicleNumber || 'Employee booking'}</div>
                                    <div style={{ color: '#94a3b8', fontSize: '0.82rem' }}>{entry.isVisitorBooking ? entry.visitorLicensePlate : 'Employee'} · Priority {entry.priorityAtRequest}</div>
                                </div>
                                <div style={{ color: '#cbd5e1', fontSize: '0.85rem' }}>
                                    {new Date(entry.requestedStartDateTime).toLocaleString()}
                                </div>
                                <div style={{ color: '#cbd5e1', fontSize: '0.85rem' }}>
                                    {entry.status === 0 ? `Position ${entry.position}` : entry.status === 1 ? 'Promoted' : 'Cancelled'}
                                </div>
                                {entry.status === 0 && (
                                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                                        <button className="btn btn-primary" type="button" onClick={() => handlePromoteWaitlist(entry.id)}>
                                            Promote
                                        </button>
                                        <button className="btn btn-secondary" type="button" onClick={() => handleCancelWaitlist(entry.id)}>
                                            Cancel
                                        </button>
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                {loading ? (
                    <div style={{ padding: '3rem', textAlign: 'center' }}><div className="spinner"></div></div>
                ) : allocations.length > 0 ? (
                    allocations.map(alloc => (
                        <div key={alloc.id} style={{ background: '#1e293b', borderRadius: '12px', padding: '1.5rem', border: '1px solid rgba(255,255,255,0.05)' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1.5rem', paddingBottom: '1rem', borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
                                <div>
                                    <h2 style={{ margin: '0 0 0.5rem 0', color: 'white', fontSize: '1.25rem' }}>{alloc.parkingSpaceTitle}</h2>
                                    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', color: '#94a3b8', fontSize: '0.85rem', alignItems: 'center' }}>
                                        {getStatusBadge(alloc.status)}
                                        <StatusBadge tone={alloc.sourceType === 1 ? 'owned' : 'leased'}>{alloc.sourceType === 1 ? 'Owned' : 'Leased'}</StatusBadge>
                                        <span>Valid: {new Date(alloc.startDate).toLocaleDateString()} - {new Date(alloc.endDate).toLocaleDateString()}</span>
                                        {alloc.leaseReference && <span>Lease: {alloc.leaseReference}</span>}
                                    </div>
                                </div>
                                <div style={{ textAlign: 'right' }}>
                                    <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: 'white' }}>{alloc.totalSlots} Slots</div>
                                    <div style={{ fontSize: '0.85rem', color: '#94a3b8' }}>
                                        {alloc.sharedSlots} Shared • {alloc.fixedSlots} Fixed
                                    </div>
                                </div>
                            </div>
                            
                            {/* Policy Section */}
                            <div style={{ marginBottom: '1rem' }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                                    <h3 style={{ fontSize: '1rem', color: '#cbd5e1', margin: 0 }}>Booking Policy</h3>
                                    {alloc.status === 1 && (
                                        <button 
                                            onClick={() => setPolicyModalObj(JSON.parse(JSON.stringify(alloc)))}
                                            style={{ background: 'transparent', border: 'none', color: '#8b5cf6', cursor: 'pointer', fontSize: '0.85rem', fontWeight: '600' }}
                                        >
                                            Edit Policy
                                        </button>
                                    )}
                                </div>
                                {alloc.policy ? (
                                    <div style={{ display: 'flex', gap: '1.5rem', background: 'rgba(255,255,255,0.02)', padding: '1rem', borderRadius: '8px' }}>
                                        <div>
                                            <span style={{ color: '#64748b', fontSize: '0.8rem', display: 'block' }}>Max/Day</span>
                                            <strong className="text-white">{alloc.policy.maxBookingsPerEmployeePerDay}</strong>
                                        </div>
                                        <div>
                                            <span style={{ color: '#64748b', fontSize: '0.8rem', display: 'block' }}>Max/Week</span>
                                            <strong className="text-white">{alloc.policy.maxBookingsPerEmployeePerWeek}</strong>
                                        </div>
                                        <div>
                                            <span style={{ color: '#64748b', fontSize: '0.8rem', display: 'block' }}>Min Priority</span>
                                            <strong className="text-white">{alloc.policy.priorityThreshold}</strong>
                                        </div>
                                        <div>
                                            <span style={{ color: '#64748b', fontSize: '0.8rem', display: 'block' }}>Allowed Hours</span>
                                            <strong className="text-white">
                                                {formatPolicyTime(alloc.policy.allowedStartTime, '07:00')} - {formatPolicyTime(alloc.policy.allowedEndTime, '22:00')}
                                            </strong>
                                        </div>
                                        <div>
                                            <span style={{ color: '#64748b', fontSize: '0.8rem', display: 'block' }}>Weekends</span>
                                            <strong className="text-white">{alloc.policy.allowWeekends ? 'Yes' : 'No'}</strong>
                                        </div>
                                    </div>
                                ) : (
                                    <span style={{ color: '#64748b', fontSize: '0.85rem' }}>No policy applied (Default rules)</span>
                                )}
                            </div>

                            {/* Fixed Slots Section */}
                            {alloc.fixedSlots > 0 && (
                                <div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                                        <h3 style={{ fontSize: '1rem', color: '#cbd5e1', margin: 0 }}>Fixed Assignments</h3>
                                        {alloc.status === 1 && (
                                            <button 
                                                onClick={() => setFixedSlotModalObj({ allocationId: alloc.id, allocationTitle: alloc.parkingSpaceTitle, membershipId: '', slotNumber: '' })}
                                                style={{ background: 'transparent', border: 'none', color: '#38bdf8', cursor: 'pointer', fontSize: '0.85rem', fontWeight: '600' }}
                                            >
                                                Assign Slot
                                            </button>
                                        )}
                                    </div>
                                    {alloc.fixedAssignments?.length > 0 ? (
                                        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                                            {alloc.fixedAssignments.map((fa, idx) => (
                                                <div key={idx} style={{ background: 'rgba(56, 189, 248, 0.1)', border: '1px solid rgba(56, 189, 248, 0.2)', padding: '6px 12px', borderRadius: '20px', fontSize: '0.8rem', color: '#cbd5e1', display: 'inline-flex', gap: '8px', alignItems: 'center' }}>
                                                    Slot <strong>{fa.slotNumber}</strong> · {fa.userName}
                                                    {alloc.status === 1 && (
                                                        <button
                                                            type="button"
                                                            onClick={() => handleRemoveFixedSlot(alloc.id, fa.membershipId)}
                                                            aria-label={`Remove slot ${fa.slotNumber}`}
                                                            style={{ background: 'transparent', border: 'none', color: '#f87171', cursor: 'pointer', fontWeight: 700, padding: 0 }}
                                                        >
                                                            x
                                                        </button>
                                                    )}
                                                </div>
                                            ))}
                                        </div>
                                    ) : (
                                        <span style={{ color: '#64748b', fontSize: '0.85rem' }}>No fixed slots assigned yet.</span>
                                    )}
                                </div>
                            )}
                        </div>
                    ))
                ) : (
                    <div style={{ background: '#1e293b', borderRadius: '12px', padding: '3rem', textAlign: 'center', color: '#94a3b8', border: '1px solid rgba(255,255,255,0.05)' }}>
                        <p style={{ marginBottom: '1rem' }}>No parking allocations found for your company.</p>
                        <button onClick={() => navigate('/search')} className="btn btn-primary" style={{ padding: '0.5rem 1rem' }}>Find Parking Spaces</button>
                    </div>
                )}
            </div>

            {sourceModalOpen && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.72)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }}>
                    <div style={{ background: '#1e293b', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '8px', padding: '1.5rem', width: '100%', maxWidth: '720px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem', marginBottom: '1rem' }}>
                            <div>
                                <h2 style={{ color: 'white', margin: 0, fontSize: '1.25rem' }}>Choose Allocation Source</h2>
                                <p style={{ color: '#94a3b8', margin: '0.35rem 0 0' }}>Use internal inventory immediately or request a vendor lease for approval.</p>
                            </div>
                            <button className="btn btn-secondary" type="button" onClick={() => setSourceModalOpen(false)}>Close</button>
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1rem' }}>
                            <button
                                type="button"
                                onClick={() => navigate('/corporate/parking-spaces')}
                                style={{ textAlign: 'left', background: '#0f172a', border: '1px solid rgba(167, 139, 250, 0.35)', borderRadius: '8px', padding: '1rem', color: 'white', cursor: 'pointer' }}
                            >
                                <div style={{ fontWeight: 700, marginBottom: '0.35rem' }}>Use Company-Owned Parking</div>
                                <div style={{ color: '#cbd5e1', fontSize: '0.9rem' }}>Create an active allocation from inventory without vendor approval.</div>
                            </button>
                            <button
                                type="button"
                                onClick={() => navigate('/search')}
                                style={{ textAlign: 'left', background: '#0f172a', border: '1px solid rgba(56, 189, 248, 0.35)', borderRadius: '8px', padding: '1rem', color: 'white', cursor: 'pointer' }}
                            >
                                <div style={{ fontWeight: 700, marginBottom: '0.35rem' }}>Request Leased Parking</div>
                                <div style={{ color: '#cbd5e1', fontSize: '0.9rem' }}>Browse vendor spaces and submit a lease request for owner approval.</div>
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Policy Modal */}
            {policyModalObj && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#1e293b', width: '100%', maxWidth: '450px', borderRadius: '12px', padding: '2rem', border: '1px solid rgba(255,255,255,0.1)' }}>
                        <h2 style={{ marginBottom: '1.5rem', color: 'white' }}>Edit Policy - {policyModalObj.parkingSpaceTitle}</h2>
                        <form onSubmit={handleUpdatePolicy}>
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1.5rem' }}>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1', fontSize: '0.9rem' }}>Max/Day</label>
                                    <input 
                                        type="number" min="1" max="100" required
                                        value={policyModalObj.policy?.maxBookingsPerEmployeePerDay || 1} 
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, maxBookingsPerEmployeePerDay: e.target.value }})} 
                                        style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                    />
                                </div>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1', fontSize: '0.9rem' }}>Max/Week</label>
                                    <input 
                                        type="number" min="1" max="500" required
                                        value={policyModalObj.policy?.maxBookingsPerEmployeePerWeek || 5} 
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, maxBookingsPerEmployeePerWeek: e.target.value }})} 
                                        style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                    />
                                </div>
                            </div>
                            
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1', fontSize: '0.9rem' }}>Required Priority Level</label>
                                <input 
                                    type="number" min="1" max="10" required
                                    value={policyModalObj.policy?.priorityThreshold || 1} 
                                    onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, priorityThreshold: e.target.value }})} 
                                    style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                />
                                <small style={{ color: '#64748b', marginTop: '4px', display: 'block' }}>Only employees with this priority or higher can book here.</small>
                            </div>

                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1.5rem' }}>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1', fontSize: '0.9rem' }}>Allowed Start</label>
                                    <input
                                        type="time"
                                        required
                                        value={formatPolicyTime(policyModalObj.policy?.allowedStartTime, '07:00')}
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, allowedStartTime: e.target.value }})}
                                        style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                    />
                                </div>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1', fontSize: '0.9rem' }}>Allowed End</label>
                                    <input
                                        type="time"
                                        required
                                        value={formatPolicyTime(policyModalObj.policy?.allowedEndTime, '22:00')}
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, allowedEndTime: e.target.value }})}
                                        style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                    />
                                </div>
                            </div>

                            <div className="form-group" style={{ marginBottom: '2rem' }}>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '10px', color: '#cbd5e1', fontSize: '0.9rem', cursor: 'pointer' }}>
                                    <input 
                                        type="checkbox"
                                        checked={policyModalObj.policy?.allowWeekends || false}
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, allowWeekends: e.target.checked }})}
                                        style={{ width: '18px', height: '18px', accentColor: '#8b5cf6' }}
                                    />
                                    Allow Weekend Bookings
                                </label>
                            </div>

                            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                <button type="button" onClick={() => setPolicyModalObj(null)} className="btn btn-secondary">Cancel</button>
                                <button type="submit" className="btn btn-primary" disabled={updatingPolicy}>
                                    {updatingPolicy ? 'Saving...' : 'Save Policy'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Assign Fixed Slot Modal */}
            {fixedSlotModalObj && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#1e293b', width: '100%', maxWidth: '400px', borderRadius: '12px', padding: '2rem', border: '1px solid rgba(255,255,255,0.1)' }}>
                        <h2 style={{ marginBottom: '1.5rem', color: 'white' }}>Assign Fixed Slot</h2>
                        <p style={{ color: '#94a3b8', fontSize: '0.85rem', marginBottom: '1.5rem' }}>{fixedSlotModalObj.allocationTitle}</p>
                        
                        <form onSubmit={handleAssignFixedSlot}>
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1', fontSize: '0.9rem' }}>Select Employee</label>
                                <select 
                                    value={fixedSlotModalObj.membershipId} 
                                    onChange={(e) => setFixedSlotModalObj({ ...fixedSlotModalObj, membershipId: e.target.value })}
                                    required
                                    style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                >
                                    <option value="" disabled>-- Select a member --</option>
                                    {members.map(m => (
                                        <option key={m.id} value={m.id}>{m.userName} ({m.userEmail})</option>
                                    ))}
                                </select>
                            </div>
                            
                            <div className="form-group" style={{ marginBottom: '2rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1', fontSize: '0.9rem' }}>Slot Number</label>
                                <input 
                                    type="number" min="1" required
                                    value={fixedSlotModalObj.slotNumber} 
                                    onChange={(e) => setFixedSlotModalObj({ ...fixedSlotModalObj, slotNumber: e.target.value })} 
                                    style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                    placeholder="e.g. 1"
                                />
                            </div>

                            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                <button type="button" onClick={() => setFixedSlotModalObj(null)} className="btn btn-secondary">Cancel</button>
                                <button type="submit" className="btn btn-primary" disabled={assigningSlot || !fixedSlotModalObj.membershipId}>
                                    {assigningSlot ? 'Assigning...' : 'Assign Slot'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};

export default CompanyAllocations;
