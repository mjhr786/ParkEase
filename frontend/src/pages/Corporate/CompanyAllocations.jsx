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

const toDateInput = (value) => {
    if (!value) return '';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return String(value).slice(0, 10);
    return d.toISOString().slice(0, 10);
};

const daysUntil = (value) => {
    const end = new Date(value);
    if (Number.isNaN(end.getTime())) return null;
    const ms = end.getTime() - Date.now();
    return Math.ceil(ms / (1000 * 60 * 60 * 24));
};

const formatMoney = (value) => {
    const n = Number(value);
    if (Number.isNaN(n)) return '—';
    return n.toLocaleString(undefined, { style: 'currency', currency: 'INR', maximumFractionDigits: 0 });
};

const badgeStyles = {
    owned: { background: 'rgba(167, 139, 250, 0.14)', color: 'var(--color-secondary)', border: '1px solid rgba(167, 139, 250, 0.35)' },
    leased: { background: 'rgba(56, 189, 248, 0.12)', color: 'var(--color-accent-light)', border: '1px solid rgba(56, 189, 248, 0.32)' },
    pending: { background: 'rgba(245, 158, 11, 0.12)', color: 'var(--color-warning)', border: '1px solid rgba(245, 158, 11, 0.32)' },
    active: { background: 'rgba(16, 185, 129, 0.12)', color: 'var(--color-success)', border: '1px solid rgba(16, 185, 129, 0.32)' },
    expired: { background: 'rgba(148, 163, 184, 0.12)', color: 'var(--color-text-secondary)', border: '1px solid rgba(148, 163, 184, 0.28)' },
    rejected: { background: 'rgba(239, 68, 68, 0.12)', color: 'var(--color-error)', border: '1px solid rgba(239, 68, 68, 0.3)' }
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

    // Modal state for Lease / Contract terms
    const [contractModalObj, setContractModalObj] = useState(null);
    const [updatingContract, setUpdatingContract] = useState(false);

    // Modal state for Fixed Slot Assignment
    const [fixedSlotModalObj, setFixedSlotModalObj] = useState(null);
    const [assigningSlot, setAssigningSlot] = useState(false);
    const [members, setMembers] = useState([]);

    // Corporate book (employee/visitor) — moved off marketplace ParkingDetails (PR8)
    const [bookModalObj, setBookModalObj] = useState(null);
    const [bookingSubmitting, setBookingSubmitting] = useState(false);

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
            const [response, memRes, waitlistRes] = await Promise.all([
                corporateService.getAllocations(),
                corporateService.getMembers(1, 100),
                corporateService.getWaitlist(),
            ]);
            if (response.success && response.data) {
                setAllocations(response.data);
            } else {
                toast.error(response.message || "Failed to load allocations");
            }
            if (memRes.success && memRes.data) {
                setMembers(memRes.data.members?.filter(m => m.isActive) || []);
            }
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

    const openContractModal = (alloc) => {
        setContractModalObj({
            id: alloc.id,
            parkingSpaceTitle: alloc.parkingSpaceTitle,
            monthlyRate: alloc.monthlyRate ?? 0,
            startDate: toDateInput(alloc.startDate),
            endDate: toDateInput(alloc.endDate),
            leaseReference: alloc.leaseReference || '',
            sourceType: alloc.sourceType,
        });
    };

    const handleUpdateContract = async (e) => {
        e.preventDefault();
        if (!contractModalObj) return;
        if (contractModalObj.endDate <= contractModalObj.startDate) {
            toast.error('End date must be after start date.');
            return;
        }
        setUpdatingContract(true);
        try {
            const payload = {
                monthlyRate: parseFloat(contractModalObj.monthlyRate),
                startDate: new Date(`${contractModalObj.startDate}T00:00:00.000Z`).toISOString(),
                endDate: new Date(`${contractModalObj.endDate}T23:59:59.000Z`).toISOString(),
                leaseReference: contractModalObj.leaseReference?.trim() || null,
            };
            const response = await corporateService.updateAllocationContract(contractModalObj.id, payload);
            if (response.success) {
                toast.success('Contract terms updated.');
                setContractModalObj(null);
                loadAllocations();
            } else {
                toast.error(response.message || 'Failed to update contract terms');
            }
        } catch {
            toast.error('An error occurred while updating contract terms');
        } finally {
            setUpdatingContract(false);
        }
    };

    const vehicleClassLabel = (vc) => {
        if (vc === 1 || vc === '1' || vc === 'TwoWheeler') return '2W';
        return '4W';
    };

    const handleAssignFixedSlot = async (e) => {
        e.preventDefault();
        setAssigningSlot(true);
        try {
            const payload = {
                membershipId: fixedSlotModalObj.membershipId,
                slotNumber: parseInt(fixedSlotModalObj.slotNumber, 10),
                vehicleClass: parseInt(fixedSlotModalObj.vehicleClass, 10) || 2
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

    const openBookModal = (alloc) => {
        setBookModalObj({
            allocationId: alloc.id,
            parkingSpaceTitle: alloc.parkingSpaceTitle,
            isVisitor: false,
            startDateTime: '',
            endDateTime: '',
            vehicleType: 0,
            vehicleNumber: '',
            visitorName: '',
            visitorPlate: '',
        });
    };

    const handleCorporateBook = async (e) => {
        e.preventDefault();
        if (!bookModalObj) return;
        if (!bookModalObj.startDateTime || !bookModalObj.endDateTime) {
            toast.error('Select start and end date/time.');
            return;
        }
        const startIso = new Date(bookModalObj.startDateTime).toISOString();
        const endIso = new Date(bookModalObj.endDateTime).toISOString();
        if (endIso <= startIso) {
            toast.error('End must be after start.');
            return;
        }

        setBookingSubmitting(true);
        try {
            let res;
            if (bookModalObj.isVisitor) {
                if (!bookModalObj.visitorName?.trim() || !bookModalObj.visitorPlate?.trim()) {
                    toast.error('Visitor name and license plate are required.');
                    setBookingSubmitting(false);
                    return;
                }
                res = await corporateService.bookVisitorParking({
                    allocationId: bookModalObj.allocationId,
                    startDateTime: startIso,
                    endDateTime: endIso,
                    visitorName: bookModalObj.visitorName.trim(),
                    visitorLicensePlate: bookModalObj.visitorPlate.trim(),
                    accessExpiry: endIso,
                });
            } else {
                res = await corporateService.bookEmployeeParking({
                    allocationId: bookModalObj.allocationId,
                    startDateTime: startIso,
                    endDateTime: endIso,
                    vehicleType: parseInt(bookModalObj.vehicleType, 10) || 0,
                    vehicleNumber: bookModalObj.vehicleNumber || null,
                });
            }

            if (res.success) {
                toast.success(
                    res.data?.waitlist
                        ? 'Added to waitlist based on allocation policy.'
                        : 'Corporate booking confirmed.'
                );
                setBookModalObj(null);
                loadAllocations();
            } else {
                toast.error(res.message || 'Corporate booking failed');
            }
        } catch {
            toast.error('An error occurred during corporate booking');
        } finally {
            setBookingSubmitting(false);
        }
    };

    if (!isCorporateMode) return null;

    return (
        <div className="container" style={{ padding: '2rem 0', color: 'var(--color-text)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
                <h1 style={{ color: 'var(--color-text-primary)', display: 'flex', alignItems: 'center', gap: '10px' }}>
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
                <div style={{ background: 'var(--color-surface)', borderRadius: '12px', padding: '1.5rem', marginBottom: '1.5rem', border: '1px solid var(--color-border)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
                        <div>
                            <h2 style={{ margin: '0 0 0.35rem 0', color: 'var(--color-text-primary)', fontSize: '1.15rem' }}>Waitlist</h2>
                            <p style={{ margin: 0, color: 'var(--color-text-secondary)', fontSize: '0.82rem' }}>
                                Auto-promotion runs in the background when a shared slot opens for the queue head. You can still promote manually.
                            </p>
                        </div>
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                        {waitlist.map(entry => (
                            <div key={entry.id} style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr 1fr auto', gap: '1rem', alignItems: 'center', background: 'rgba(255,255,255,0.02)', padding: '0.85rem', borderRadius: '8px' }}>
                                <div>
                                    <div style={{ color: 'var(--color-text-primary)', fontWeight: 600 }}>{entry.isVisitorBooking ? entry.visitorName : entry.vehicleNumber || 'Employee booking'}</div>
                                    <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.82rem' }}>{entry.isVisitorBooking ? entry.visitorLicensePlate : 'Employee'} · Priority {entry.priorityAtRequest}</div>
                                </div>
                                <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
                                    {new Date(entry.requestedStartDateTime).toLocaleString()}
                                </div>
                                <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
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
                        <div key={alloc.id} style={{ background: 'var(--color-surface)', borderRadius: '12px', padding: '1.5rem', border: '1px solid var(--color-border)' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1.5rem', paddingBottom: '1rem', borderBottom: '1px solid var(--color-border)' }}>
                                <div>
                                    <h2 style={{ margin: '0 0 0.5rem 0', color: 'var(--color-text-primary)', fontSize: '1.25rem' }}>{alloc.parkingSpaceTitle}</h2>
                                    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', color: 'var(--color-text-secondary)', fontSize: '0.85rem', alignItems: 'center' }}>
                                        {getStatusBadge(alloc.status)}
                                        <StatusBadge tone={alloc.sourceType === 1 ? 'owned' : 'leased'}>{alloc.sourceType === 1 ? 'Owned' : 'Leased'}</StatusBadge>
                                        {(() => {
                                            const d = daysUntil(alloc.endDate);
                                            if (alloc.status === 1 && d != null && d <= 30) {
                                                return (
                                                    <StatusBadge tone={d <= 7 ? 'rejected' : 'pending'}>
                                                        {d < 0 ? 'Past end date' : `Expires in ${d}d`}
                                                    </StatusBadge>
                                                );
                                            }
                                            return null;
                                        })()}
                                    </div>
                                    <div style={{ marginTop: '0.65rem', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '0.65rem', maxWidth: '640px' }}>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem', display: 'block' }}>Contract window</span>
                                            <strong style={{ color: 'var(--color-text-primary)', fontSize: '0.85rem' }}>
                                                {new Date(alloc.startDate).toLocaleDateString()} – {new Date(alloc.endDate).toLocaleDateString()}
                                            </strong>
                                        </div>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem', display: 'block' }}>Monthly rate</span>
                                            <strong style={{ color: 'var(--color-text-primary)', fontSize: '0.85rem' }}>{formatMoney(alloc.monthlyRate)}</strong>
                                        </div>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem', display: 'block' }}>
                                                {alloc.sourceType === 1 ? 'Internal ref' : 'Lease ref'}
                                            </span>
                                            <strong style={{ color: 'var(--color-text-primary)', fontSize: '0.85rem' }}>{alloc.leaseReference || '—'}</strong>
                                        </div>
                                        {alloc.sourceType !== 1 && (
                                            <div>
                                                <span style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem', display: 'block' }}>Vendor</span>
                                                <strong style={{ color: 'var(--color-text-primary)', fontSize: '0.85rem' }}>{alloc.vendorName || 'Parking owner'}</strong>
                                            </div>
                                        )}
                                    </div>
                                </div>
                                <div style={{ textAlign: 'right' }}>
                                    <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: 'var(--color-text-primary)' }}>{alloc.totalSlots} Slots</div>
                                    <div style={{ fontSize: '0.85rem', color: 'var(--color-text-secondary)', marginBottom: '0.35rem' }}>
                                        {alloc.sharedSlots} Shared • {alloc.fixedSlots} Fixed
                                    </div>
                                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '0.35rem' }}>
                                        {alloc.status === 1 && (
                                            <button
                                                type="button"
                                                className="btn btn-primary"
                                                onClick={() => openBookModal(alloc)}
                                                style={{ padding: '0.4rem 0.85rem', fontSize: '0.85rem' }}
                                            >
                                                Book space
                                            </button>
                                        )}
                                        {(alloc.status === 0 || alloc.status === 1) && (
                                            <button
                                                type="button"
                                                onClick={() => openContractModal(alloc)}
                                                style={{ background: 'transparent', border: 'none', color: 'var(--color-accent-light)', cursor: 'pointer', fontSize: '0.85rem', fontWeight: '600' }}
                                            >
                                                Edit Contract
                                            </button>
                                        )}
                                    </div>
                                </div>
                            </div>

                            {/* Policy Section */}
                            <div style={{ marginBottom: '1rem' }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                                    <h3 style={{ fontSize: '1rem', color: 'var(--color-text-secondary)', margin: 0 }}>Booking Policy</h3>
                                    {alloc.status === 1 && (
                                        <button 
                                            onClick={() => setPolicyModalObj(JSON.parse(JSON.stringify(alloc)))}
                                            style={{ background: 'transparent', border: 'none', color: 'var(--color-secondary)', cursor: 'pointer', fontSize: '0.85rem', fontWeight: '600' }}
                                        >
                                            Edit Policy
                                        </button>
                                    )}
                                </div>
                                {alloc.policy ? (
                                    <div style={{ display: 'flex', gap: '1.5rem', background: 'rgba(255,255,255,0.02)', padding: '1rem', borderRadius: '8px' }}>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.8rem', display: 'block' }}>Max/Day</span>
                                            <strong className="text-white">{alloc.policy.maxBookingsPerEmployeePerDay}</strong>
                                        </div>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.8rem', display: 'block' }}>Max/Week</span>
                                            <strong className="text-white">{alloc.policy.maxBookingsPerEmployeePerWeek}</strong>
                                        </div>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.8rem', display: 'block' }}>Min Priority</span>
                                            <strong className="text-white">{alloc.policy.priorityThreshold}</strong>
                                        </div>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.8rem', display: 'block' }}>Allowed Hours</span>
                                            <strong className="text-white">
                                                {formatPolicyTime(alloc.policy.allowedStartTime, '07:00')} - {formatPolicyTime(alloc.policy.allowedEndTime, '22:00')}
                                            </strong>
                                        </div>
                                        <div>
                                            <span style={{ color: 'var(--color-text-muted)', fontSize: '0.8rem', display: 'block' }}>Weekends</span>
                                            <strong className="text-white">{alloc.policy.allowWeekends ? 'Yes' : 'No'}</strong>
                                        </div>
                                    </div>
                                ) : (
                                    <span style={{ color: 'var(--color-text-muted)', fontSize: '0.85rem' }}>No policy applied (Default rules)</span>
                                )}
                            </div>

                            {/* Fixed Slots Section */}
                            {alloc.fixedSlots > 0 && (
                                <div>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                                        <h3 style={{ fontSize: '1rem', color: 'var(--color-text-secondary)', margin: 0 }}>Fixed Assignments</h3>
                                        {alloc.status === 1 && (
                                            <button 
                                                onClick={() => setFixedSlotModalObj({ allocationId: alloc.id, allocationTitle: alloc.parkingSpaceTitle, membershipId: '', slotNumber: '', vehicleClass: 2 })}
                                                style={{ background: 'transparent', border: 'none', color: 'var(--color-accent-light)', cursor: 'pointer', fontSize: '0.85rem', fontWeight: '600' }}
                                            >
                                                Assign Slot
                                            </button>
                                        )}
                                    </div>
                                    {alloc.fixedAssignments?.length > 0 ? (
                                        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                                            {alloc.fixedAssignments.map((fa, idx) => (
                                                <div key={idx} style={{ background: 'rgba(56, 189, 248, 0.1)', border: '1px solid rgba(56, 189, 248, 0.2)', padding: '6px 12px', borderRadius: '20px', fontSize: '0.8rem', color: 'var(--color-text-secondary)', display: 'inline-flex', gap: '8px', alignItems: 'center' }}>
                                                    {vehicleClassLabel(fa.vehicleClass)} Slot <strong>{fa.slotNumber}</strong> · {fa.userName}
                                                    {alloc.status === 1 && (
                                                        <button
                                                            type="button"
                                                            onClick={() => handleRemoveFixedSlot(alloc.id, fa.membershipId)}
                                                            aria-label={`Remove slot ${fa.slotNumber}`}
                                                            style={{ background: 'transparent', border: 'none', color: 'var(--color-error)', cursor: 'pointer', fontWeight: 700, padding: 0 }}
                                                        >
                                                            x
                                                        </button>
                                                    )}
                                                </div>
                                            ))}
                                        </div>
                                    ) : (
                                        <span style={{ color: 'var(--color-text-muted)', fontSize: '0.85rem' }}>No fixed slots assigned yet.</span>
                                    )}
                                </div>
                            )}
                        </div>
                    ))
                ) : (
                    <div style={{ background: 'var(--color-surface)', borderRadius: '12px', padding: '3rem', textAlign: 'center', color: 'var(--color-text-secondary)', border: '1px solid var(--color-border)' }}>
                        <p style={{ marginBottom: '1rem' }}>No parking allocations found for your company.</p>
                        <button onClick={() => navigate('/corporate/lease-browse')} className="btn btn-primary" style={{ padding: '0.5rem 1rem' }}>Browse vendor lots to lease</button>
                    </div>
                )}
            </div>

            {sourceModalOpen && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.72)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }}>
                    <div style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: '8px', padding: '1.5rem', width: '100%', maxWidth: '720px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem', marginBottom: '1rem' }}>
                            <div>
                                <h2 style={{ color: 'var(--color-text-primary)', margin: 0, fontSize: '1.25rem' }}>Choose Allocation Source</h2>
                                <p style={{ color: 'var(--color-text-secondary)', margin: '0.35rem 0 0' }}>Use internal inventory immediately or request a vendor lease for approval.</p>
                            </div>
                            <button className="btn btn-secondary" type="button" onClick={() => setSourceModalOpen(false)}>Close</button>
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '1rem' }}>
                            <button
                                type="button"
                                onClick={() => navigate('/corporate/parking-spaces')}
                                style={{ textAlign: 'left', background: 'var(--color-bg-primary)', border: '1px solid rgba(167, 139, 250, 0.35)', borderRadius: '8px', padding: '1rem', color: 'var(--color-text-primary)', cursor: 'pointer' }}
                            >
                                <div style={{ fontWeight: 700, marginBottom: '0.35rem' }}>Use Company-Owned Parking</div>
                                <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Create an active allocation from inventory without vendor approval.</div>
                            </button>
                            <button
                                type="button"
                                onClick={() => navigate('/corporate/lease-browse')}
                                style={{ textAlign: 'left', background: 'var(--color-bg-primary)', border: '1px solid rgba(56, 189, 248, 0.35)', borderRadius: '8px', padding: '1rem', color: 'var(--color-text-primary)', cursor: 'pointer' }}
                            >
                                <div style={{ fontWeight: 700, marginBottom: '0.35rem' }}>Request Leased Parking</div>
                                <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Browse vendor spaces (Admin) and submit a lease request for owner approval.</div>
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Policy Modal */}
            {policyModalObj && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: 'var(--color-surface)', width: '100%', maxWidth: '450px', borderRadius: '12px', padding: '2rem', border: '1px solid var(--color-border)' }}>
                        <h2 style={{ marginBottom: '1.5rem', color: 'var(--color-text-primary)' }}>Edit Policy - {policyModalObj.parkingSpaceTitle}</h2>
                        <form onSubmit={handleUpdatePolicy}>
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1.5rem' }}>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Max/Day</label>
                                    <input 
                                        type="number" min="1" max="100" required
                                        value={policyModalObj.policy?.maxBookingsPerEmployeePerDay || 1} 
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, maxBookingsPerEmployeePerDay: e.target.value }})} 
                                        style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                    />
                                </div>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Max/Week</label>
                                    <input 
                                        type="number" min="1" max="500" required
                                        value={policyModalObj.policy?.maxBookingsPerEmployeePerWeek || 5} 
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, maxBookingsPerEmployeePerWeek: e.target.value }})} 
                                        style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                    />
                                </div>
                            </div>
                            
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Required Priority Level</label>
                                <input 
                                    type="number" min="1" max="10" required
                                    value={policyModalObj.policy?.priorityThreshold || 1} 
                                    onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, priorityThreshold: e.target.value }})} 
                                    style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                />
                                <small style={{ color: 'var(--color-text-muted)', marginTop: '4px', display: 'block' }}>Only employees with this priority or higher can book here.</small>
                            </div>

                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1.5rem' }}>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Allowed Start</label>
                                    <input
                                        type="time"
                                        required
                                        value={formatPolicyTime(policyModalObj.policy?.allowedStartTime, '07:00')}
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, allowedStartTime: e.target.value }})}
                                        style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                    />
                                </div>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Allowed End</label>
                                    <input
                                        type="time"
                                        required
                                        value={formatPolicyTime(policyModalObj.policy?.allowedEndTime, '22:00')}
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, allowedEndTime: e.target.value }})}
                                        style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                    />
                                </div>
                            </div>

                            <div className="form-group" style={{ marginBottom: '2rem' }}>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '10px', color: 'var(--color-text-secondary)', fontSize: '0.9rem', cursor: 'pointer' }}>
                                    <input 
                                        type="checkbox"
                                        checked={policyModalObj.policy?.allowWeekends || false}
                                        onChange={(e) => setPolicyModalObj({ ...policyModalObj, policy: { ...policyModalObj.policy, allowWeekends: e.target.checked }})}
                                        style={{ width: '18px', height: '18px', accentColor: 'var(--color-secondary)' }}
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

            {/* Contract / Lease terms modal */}
            {contractModalObj && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: 'var(--color-surface)', width: '100%', maxWidth: '480px', borderRadius: '12px', padding: '2rem', border: '1px solid var(--color-border)' }}>
                        <h2 style={{ marginBottom: '0.5rem', color: 'var(--color-text-primary)' }}>Edit Contract</h2>
                        <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem', marginBottom: '1.5rem' }}>{contractModalObj.parkingSpaceTitle}</p>
                        <form onSubmit={handleUpdateContract}>
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1rem' }}>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Start Date</label>
                                    <input
                                        type="date"
                                        required
                                        value={contractModalObj.startDate}
                                        onChange={(e) => setContractModalObj({ ...contractModalObj, startDate: e.target.value })}
                                        style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                    />
                                </div>
                                <div className="form-group">
                                    <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>End Date</label>
                                    <input
                                        type="date"
                                        required
                                        value={contractModalObj.endDate}
                                        onChange={(e) => setContractModalObj({ ...contractModalObj, endDate: e.target.value })}
                                        style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                    />
                                </div>
                            </div>
                            <div className="form-group" style={{ marginBottom: '1rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Monthly Rate (INR)</label>
                                <input
                                    type="number"
                                    min="0"
                                    step="0.01"
                                    required
                                    value={contractModalObj.monthlyRate}
                                    onChange={(e) => setContractModalObj({ ...contractModalObj, monthlyRate: e.target.value })}
                                    style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                />
                            </div>
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
                                    {contractModalObj.sourceType === 1 ? 'Internal Reference' : 'Lease Reference'}
                                </label>
                                <input
                                    type="text"
                                    maxLength={100}
                                    placeholder="PO / contract number (optional)"
                                    value={contractModalObj.leaseReference}
                                    onChange={(e) => setContractModalObj({ ...contractModalObj, leaseReference: e.target.value })}
                                    style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                />
                            </div>
                            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                <button type="button" onClick={() => setContractModalObj(null)} className="btn btn-secondary">Cancel</button>
                                <button type="submit" className="btn btn-primary" disabled={updatingContract}>
                                    {updatingContract ? 'Saving...' : 'Save Contract'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Corporate Book Modal (employee / visitor) */}
            {bookModalObj && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }}>
                    <div style={{ background: 'var(--color-surface)', width: '100%', maxWidth: '480px', borderRadius: '12px', padding: '2rem', border: '1px solid var(--color-border)', maxHeight: '90vh', overflowY: 'auto' }}>
                        <h2 style={{ marginBottom: '0.35rem', color: 'var(--color-text-primary)' }}>Book corporate space</h2>
                        <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem', marginBottom: '1.25rem' }}>{bookModalObj.parkingSpaceTitle}</p>
                        <form onSubmit={handleCorporateBook}>
                            <div className="form-group" style={{ marginBottom: '1rem' }}>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', cursor: 'pointer', color: 'var(--color-text-primary)' }}>
                                    <input
                                        type="checkbox"
                                        checked={bookModalObj.isVisitor}
                                        onChange={(e) => setBookModalObj({ ...bookModalObj, isVisitor: e.target.checked })}
                                    />
                                    This is for a visitor
                                </label>
                            </div>
                            <div className="form-group" style={{ marginBottom: '1rem' }}>
                                <label style={{ display: 'block', marginBottom: 8, color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Start</label>
                                <input
                                    type="datetime-local"
                                    required
                                    value={bookModalObj.startDateTime}
                                    onChange={(e) => setBookModalObj({ ...bookModalObj, startDateTime: e.target.value })}
                                    style={{ width: '100%', padding: 10, background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: 6, color: 'var(--color-text-primary)' }}
                                />
                            </div>
                            <div className="form-group" style={{ marginBottom: '1rem' }}>
                                <label style={{ display: 'block', marginBottom: 8, color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>End</label>
                                <input
                                    type="datetime-local"
                                    required
                                    value={bookModalObj.endDateTime}
                                    onChange={(e) => setBookModalObj({ ...bookModalObj, endDateTime: e.target.value })}
                                    style={{ width: '100%', padding: 10, background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: 6, color: 'var(--color-text-primary)' }}
                                />
                            </div>
                            {bookModalObj.isVisitor ? (
                                <>
                                    <div className="form-group" style={{ marginBottom: '1rem' }}>
                                        <label style={{ display: 'block', marginBottom: 8, color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Visitor name</label>
                                        <input
                                            type="text"
                                            required
                                            value={bookModalObj.visitorName}
                                            onChange={(e) => setBookModalObj({ ...bookModalObj, visitorName: e.target.value })}
                                            style={{ width: '100%', padding: 10, background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: 6, color: 'var(--color-text-primary)' }}
                                        />
                                    </div>
                                    <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                        <label style={{ display: 'block', marginBottom: 8, color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Visitor license plate</label>
                                        <input
                                            type="text"
                                            required
                                            value={bookModalObj.visitorPlate}
                                            onChange={(e) => setBookModalObj({ ...bookModalObj, visitorPlate: e.target.value })}
                                            style={{ width: '100%', padding: 10, background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: 6, color: 'var(--color-text-primary)' }}
                                        />
                                    </div>
                                </>
                            ) : (
                                <>
                                    <div className="form-group" style={{ marginBottom: '1rem' }}>
                                        <label style={{ display: 'block', marginBottom: 8, color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Vehicle type</label>
                                        <select
                                            value={bookModalObj.vehicleType}
                                            onChange={(e) => setBookModalObj({ ...bookModalObj, vehicleType: e.target.value })}
                                            style={{ width: '100%', padding: 10, background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: 6, color: 'var(--color-text-primary)' }}
                                        >
                                            <option value={0}>Car</option>
                                            <option value={1}>Motorcycle</option>
                                            <option value={2}>SUV</option>
                                            <option value={3}>Truck</option>
                                            <option value={4}>Van</option>
                                            <option value={5}>Electric</option>
                                        </select>
                                    </div>
                                    <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                        <label style={{ display: 'block', marginBottom: 8, color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Vehicle number (optional)</label>
                                        <input
                                            type="text"
                                            value={bookModalObj.vehicleNumber}
                                            onChange={(e) => setBookModalObj({ ...bookModalObj, vehicleNumber: e.target.value })}
                                            style={{ width: '100%', padding: 10, background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: 6, color: 'var(--color-text-primary)' }}
                                        />
                                    </div>
                                </>
                            )}
                            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                <button type="button" onClick={() => setBookModalObj(null)} className="btn btn-secondary">Cancel</button>
                                <button type="submit" className="btn btn-primary" disabled={bookingSubmitting}>
                                    {bookingSubmitting ? 'Booking…' : 'Confirm booking'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Assign Fixed Slot Modal */}
            {fixedSlotModalObj && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: 'var(--color-surface)', width: '100%', maxWidth: '400px', borderRadius: '12px', padding: '2rem', border: '1px solid var(--color-border)' }}>
                        <h2 style={{ marginBottom: '1.5rem', color: 'var(--color-text-primary)' }}>Assign Fixed Slot</h2>
                        <p style={{ color: 'var(--color-text-secondary)', fontSize: '0.85rem', marginBottom: '1.5rem' }}>{fixedSlotModalObj.allocationTitle}</p>
                        
                        <form onSubmit={handleAssignFixedSlot}>
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Select Employee</label>
                                <select 
                                    value={fixedSlotModalObj.membershipId} 
                                    onChange={(e) => setFixedSlotModalObj({ ...fixedSlotModalObj, membershipId: e.target.value })}
                                    required
                                    style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                >
                                    <option value="" disabled>-- Select a member --</option>
                                    {members.map(m => (
                                        <option key={m.id} value={m.id}>{m.userName} ({m.userEmail})</option>
                                    ))}
                                </select>
                            </div>
                            
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Vehicle class</label>
                                <select
                                    value={fixedSlotModalObj.vehicleClass ?? 2}
                                    onChange={(e) => setFixedSlotModalObj({ ...fixedSlotModalObj, vehicleClass: e.target.value })}
                                    style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
                                >
                                    <option value={2}>4-Wheeler</option>
                                    <option value={1}>2-Wheeler</option>
                                </select>
                            </div>

                            <div className="form-group" style={{ marginBottom: '2rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>Slot Number</label>
                                <input 
                                    type="number" min="1" required
                                    value={fixedSlotModalObj.slotNumber} 
                                    onChange={(e) => setFixedSlotModalObj({ ...fixedSlotModalObj, slotNumber: e.target.value })} 
                                    style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
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
