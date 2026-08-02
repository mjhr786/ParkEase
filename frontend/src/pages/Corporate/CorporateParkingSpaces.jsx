import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';
import toast from 'react-hot-toast';

const defaultSpace = {
    title: '',
    description: '',
    address: '',
    city: '',
    state: '',
    country: 'India',
    postalCode: '',
    latitude: 0,
    longitude: 0,
    parkingType: 0,
    totalSpots: 10,
    hourlyRate: 0,
    dailyRate: 0,
    weeklyRate: 0,
    monthlyRate: 0,
    openTime: '00:00:00',
    closeTime: '23:59:59',
    is24Hours: true,
    amenities: [],
    allowedVehicleTypes: [0],
    imageUrls: [],
    specialInstructions: '',
    zoneCode: ''
};

const defaultAllocation = {
    parkingSpaceId: '',
    fourWheeler: { totalSlots: 1, fixedSlots: 0, sharedSlots: 1 },
    twoWheeler: { totalSlots: 0, fixedSlots: 0, sharedSlots: 0 },
    monthlyRate: 0,
    startDate: '',
    endDate: '',
    policy: {
        maxBookingsPerEmployeePerDay: 1,
        maxBookingsPerEmployeePerWeek: 5,
        priorityThreshold: 1,
        allowedStartTime: '07:00:00',
        allowedEndTime: '22:00:00',
        allowWeekends: false
    }
};

const toDateTime = (date) => date ? `${date}T00:00:00Z` : '';

const CorporateParkingSpaces = () => {
    const { activeCompanyId, isCorporateMode } = useCompany();
    const navigate = useNavigate();
    const [spaces, setSpaces] = useState([]);
    const [loading, setLoading] = useState(true);
    const [creating, setCreating] = useState(false);
    const [updating, setUpdating] = useState(false);
    const [allocating, setAllocating] = useState(false);
    const [spaceForm, setSpaceForm] = useState(defaultSpace);
    const [editingSpace, setEditingSpace] = useState(null);
    const [allocationForm, setAllocationForm] = useState(defaultAllocation);

    useEffect(() => {
        if (!isCorporateMode) {
            navigate('/dashboard', { replace: true });
            return;
        }
        loadSpaces();
    }, [activeCompanyId, isCorporateMode, navigate]);

    const loadSpaces = async () => {
        setLoading(true);
        try {
            const response = await corporateService.getParkingSpaces();
            if (response.success && response.data) {
                setSpaces(response.data);
            } else {
                toast.error(response.message || 'Failed to load company parking spaces');
            }
        } catch (error) {
            toast.error('Could not reach server');
        } finally {
            setLoading(false);
        }
    };

    const updateSpaceForm = (field, value) => {
        setSpaceForm(prev => ({ ...prev, [field]: value }));
    };

    const toSpaceForm = (space) => ({
        title: space.title || '',
        description: space.description || '',
        address: space.address || '',
        city: space.city || '',
        state: space.state || '',
        country: space.country || 'India',
        postalCode: space.postalCode || '',
        latitude: space.latitude ?? 0,
        longitude: space.longitude ?? 0,
        parkingType: space.parkingType ?? 0,
        totalSpots: space.totalSpots || 1,
        hourlyRate: space.hourlyRate || 0,
        dailyRate: space.dailyRate || 0,
        weeklyRate: space.weeklyRate || 0,
        monthlyRate: space.monthlyRate || 0,
        openTime: String(space.openTime || '00:00:00').slice(0, 8),
        closeTime: String(space.closeTime || '23:59:59').slice(0, 8),
        is24Hours: space.is24Hours ?? true,
        amenities: space.amenities || [],
        allowedVehicleTypes: space.allowedVehicleTypes || [0],
        imageUrls: space.imageUrls || [],
        specialInstructions: space.specialInstructions || '',
        zoneCode: space.zoneCode || ''
    });

    const buildSpacePayload = () => ({
        ...spaceForm,
        latitude: parseFloat(spaceForm.latitude) || 0,
        longitude: parseFloat(spaceForm.longitude) || 0,
        parkingType: parseInt(spaceForm.parkingType),
        totalSpots: parseInt(spaceForm.totalSpots),
        hourlyRate: parseFloat(spaceForm.hourlyRate) || 0,
        dailyRate: parseFloat(spaceForm.dailyRate) || 0,
        weeklyRate: parseFloat(spaceForm.weeklyRate) || 0,
        monthlyRate: parseFloat(spaceForm.monthlyRate) || 0,
        openTime: spaceForm.is24Hours ? '00:00:00' : spaceForm.openTime,
        closeTime: spaceForm.is24Hours ? '23:59:59' : spaceForm.closeTime
    });

    const handleCreateSpace = async (event) => {
        event.preventDefault();
        setCreating(true);
        try {
            const response = await corporateService.createParkingSpace(buildSpacePayload());
            if (response.success) {
                toast.success('Company-owned parking created.');
                setSpaceForm(defaultSpace);
                loadSpaces();
            } else {
                toast.error(response.message || 'Failed to create parking space');
            }
        } catch (error) {
            toast.error('An error occurred while creating parking space');
        } finally {
            setCreating(false);
        }
    };

    const startEditSpace = (space) => {
        setEditingSpace(space);
        setSpaceForm(toSpaceForm(space));
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const cancelEditSpace = () => {
        setEditingSpace(null);
        setSpaceForm(defaultSpace);
    };

    const handleUpdateSpace = async (event) => {
        event.preventDefault();
        if (!editingSpace) return;

        setUpdating(true);
        try {
            const response = await corporateService.updateParkingSpace(editingSpace.id, buildSpacePayload());
            if (response.success) {
                toast.success('Company-owned parking updated.');
                cancelEditSpace();
                loadSpaces();
            } else {
                toast.error(response.message || 'Failed to update parking space');
            }
        } catch (error) {
            toast.error('An error occurred while updating parking space');
        } finally {
            setUpdating(false);
        }
    };

    const handleRetire = async (space) => {
        if (!window.confirm(`Retire ${space.title}? This removes it from corporate inventory once no active allocations or bookings remain.`)) return;

        try {
            const response = await corporateService.retireParkingSpace(space.id);
            if (response.success) {
                toast.success(response.message || 'Parking space retired.');
                loadSpaces();
            } else {
                toast.error(response.message || 'Failed to retire parking space');
            }
        } catch (error) {
            toast.error('An error occurred while retiring parking space');
        }
    };

    const openAllocation = (space) => {
        const fourTotal = space.totalSpots || 1;
        setAllocationForm({
            ...defaultAllocation,
            parkingSpaceId: space.id,
            fourWheeler: { totalSlots: fourTotal, fixedSlots: 0, sharedSlots: fourTotal },
            twoWheeler: { totalSlots: 0, fixedSlots: 0, sharedSlots: 0 },
            monthlyRate: space.monthlyRate || 0,
            startDate: new Date().toISOString().slice(0, 10),
            endDate: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
        });
    };

    const updatePool = (poolKey, field, value) => {
        setAllocationForm(prev => {
            const pool = { ...prev[poolKey], [field]: value };
            const total = Math.max(0, parseInt(pool.totalSlots, 10) || 0);
            const fixed = Math.min(Math.max(0, parseInt(pool.fixedSlots, 10) || 0), total);
            const shared = Math.min(Math.max(0, parseInt(pool.sharedSlots, 10) || 0), total - fixed);
            return { ...prev, [poolKey]: { totalSlots: total, fixedSlots: fixed, sharedSlots: shared } };
        });
    };

    const handleCreateAllocation = async (event) => {
        event.preventDefault();
        const fourWheeler = {
            totalSlots: parseInt(allocationForm.fourWheeler.totalSlots, 10) || 0,
            fixedSlots: parseInt(allocationForm.fourWheeler.fixedSlots, 10) || 0,
            sharedSlots: parseInt(allocationForm.fourWheeler.sharedSlots, 10) || 0
        };
        const twoWheeler = {
            totalSlots: parseInt(allocationForm.twoWheeler.totalSlots, 10) || 0,
            fixedSlots: parseInt(allocationForm.twoWheeler.fixedSlots, 10) || 0,
            sharedSlots: parseInt(allocationForm.twoWheeler.sharedSlots, 10) || 0
        };
        const combined = fourWheeler.totalSlots + twoWheeler.totalSlots;
        if (combined <= 0) {
            toast.error('At least one vehicle class pool must have capacity.');
            return;
        }
        if (fourWheeler.fixedSlots + fourWheeler.sharedSlots > fourWheeler.totalSlots
            || twoWheeler.fixedSlots + twoWheeler.sharedSlots > twoWheeler.totalSlots) {
            toast.error('Fixed plus shared cannot exceed total for each vehicle class.');
            return;
        }

        setAllocating(true);
        try {
            const payload = {
                parkingSpaceId: allocationForm.parkingSpaceId,
                fourWheeler,
                twoWheeler,
                monthlyRate: parseFloat(allocationForm.monthlyRate) || 0,
                startDate: toDateTime(allocationForm.startDate),
                endDate: toDateTime(allocationForm.endDate),
                policy: allocationForm.policy
            };

            const response = await corporateService.createOwnedAllocation(allocationForm.parkingSpaceId, payload);
            if (response.success) {
                toast.success('Owned parking allocation activated.');
                setAllocationForm(defaultAllocation);
            } else {
                toast.error(response.message || 'Failed to activate allocation');
            }
        } catch (error) {
            toast.error('An error occurred while activating allocation');
        } finally {
            setAllocating(false);
        }
    };

    const handleToggle = async (space) => {
        try {
            const response = await corporateService.toggleParkingSpace(space.id);
            if (response.success) {
                toast.success(response.message || 'Parking space updated.');
                loadSpaces();
            } else {
                toast.error(response.message || 'Failed to update parking space');
            }
        } catch (error) {
            toast.error('An error occurred while updating parking space');
        }
    };

    if (!isCorporateMode) return null;

    return (
        <div className="container" style={{ padding: '2rem 0', color: 'var(--color-text)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
                <div>
                    <h1 style={{ color: 'var(--color-text-primary)', margin: 0 }}>Corporate Parking Inventory</h1>
                    <p style={{ color: 'var(--color-text-secondary)', margin: '0.4rem 0 0 0' }}>Company-owned spaces become active allocations without vendor approval.</p>
                </div>
                <button onClick={() => navigate('/corporate/allocations')} className="btn btn-secondary">View Allocations</button>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'minmax(320px, 420px) 1fr', gap: '1.5rem', alignItems: 'start' }}>
                <form onSubmit={editingSpace ? handleUpdateSpace : handleCreateSpace} style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: '8px', padding: '1.5rem' }}>
                    <h2 style={{ margin: '0 0 1rem 0', color: 'var(--color-text-primary)', fontSize: '1.1rem' }}>{editingSpace ? 'Edit Owned Parking' : 'Add Owned Parking'}</h2>
                    <Field label="Title" value={spaceForm.title} onChange={value => updateSpaceForm('title', value)} required />
                    <Field label="Description" value={spaceForm.description} onChange={value => updateSpaceForm('description', value)} required />
                    <Field label="Address" value={spaceForm.address} onChange={value => updateSpaceForm('address', value)} required />
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                        <Field label="City" value={spaceForm.city} onChange={value => updateSpaceForm('city', value)} required />
                        <Field label="State" value={spaceForm.state} onChange={value => updateSpaceForm('state', value)} required />
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                        <Field label="Postal Code" value={spaceForm.postalCode} onChange={value => updateSpaceForm('postalCode', value)} required />
                        <Field label="Country" value={spaceForm.country} onChange={value => updateSpaceForm('country', value)} required />
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                        <Field label="Latitude" type="number" value={spaceForm.latitude} onChange={value => updateSpaceForm('latitude', value)} step="0.000001" />
                        <Field label="Longitude" type="number" value={spaceForm.longitude} onChange={value => updateSpaceForm('longitude', value)} step="0.000001" />
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                        <Field label="Total Spots" type="number" min="1" value={spaceForm.totalSpots} onChange={value => updateSpaceForm('totalSpots', value)} required />
                        <Field label="Monthly Rate" type="number" min="0" value={spaceForm.monthlyRate} onChange={value => updateSpaceForm('monthlyRate', value)} />
                    </div>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '1rem', color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>
                        <input type="checkbox" checked={spaceForm.is24Hours} onChange={e => updateSpaceForm('is24Hours', e.target.checked)} />
                        24 hours
                    </label>
                    <div style={{ display: 'flex', gap: '0.75rem' }}>
                        {editingSpace && (
                            <button className="btn btn-secondary" type="button" onClick={cancelEditSpace} style={{ flex: 1 }}>
                                Cancel
                            </button>
                        )}
                        <button className="btn btn-primary" type="submit" disabled={creating || updating} style={{ flex: 1 }}>
                            {editingSpace ? (updating ? 'Saving...' : 'Save Changes') : (creating ? 'Creating...' : 'Create Owned Parking')}
                        </button>
                    </div>
                </form>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    {loading ? (
                        <div style={{ padding: '3rem', textAlign: 'center' }}><div className="spinner"></div></div>
                    ) : spaces.length === 0 ? (
                        <div style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: '12px', padding: '2rem', color: 'var(--color-text-secondary)' }}>
                            No company-owned parking spaces yet.
                        </div>
                    ) : spaces.map(space => (
                        <div key={space.id} style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: '8px', padding: '1.25rem' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem' }}>
                                <div>
                                    <h3 style={{ color: 'var(--color-text-primary)', margin: '0 0 0.35rem 0' }}>{space.title}</h3>
                                    <div style={{ color: 'var(--color-text-secondary)', fontSize: '0.9rem' }}>{space.address}, {space.city}</div>
                                    <div style={{ display: 'flex', gap: '0.75rem', marginTop: '0.75rem', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
                                        <span>{space.totalSpots} spots</span>
                                        <span>{space.isActive ? 'Active' : 'Inactive'}</span>
                                        <span>Owned</span>
                                    </div>
                                </div>
                                <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'start' }}>
                                    <button className="btn btn-secondary" type="button" onClick={() => startEditSpace(space)}>
                                        Edit
                                    </button>
                                    <button className="btn btn-secondary" type="button" onClick={() => handleToggle(space)}>
                                        {space.isActive ? 'Deactivate' : 'Activate'}
                                    </button>
                                    <button className="btn btn-primary" type="button" disabled={!space.isActive} onClick={() => openAllocation(space)}>
                                        Allocate
                                    </button>
                                    <button className="btn btn-danger" type="button" onClick={() => handleRetire(space)}>
                                        Retire
                                    </button>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            </div>

            {allocationForm.parkingSpaceId && (
                <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }}>
                    <form onSubmit={handleCreateAllocation} style={{ background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: '12px', padding: '1.5rem', width: '100%', maxWidth: '520px' }}>
                        <h2 style={{ color: 'var(--color-text-primary)', margin: '0 0 1rem 0' }}>Activate Internal Allocation</h2>
                        <p style={{ margin: '0 0 0.5rem', fontSize: '0.85rem', color: 'var(--color-text-muted)' }}>4-Wheeler (Car / SUV)</p>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '0.75rem' }}>
                            <Field label="4W Total" type="number" min="0" value={allocationForm.fourWheeler.totalSlots} onChange={value => updatePool('fourWheeler', 'totalSlots', value)} />
                            <Field label="4W Fixed" type="number" min="0" value={allocationForm.fourWheeler.fixedSlots} onChange={value => updatePool('fourWheeler', 'fixedSlots', value)} />
                            <Field label="4W Shared" type="number" min="0" value={allocationForm.fourWheeler.sharedSlots} onChange={value => updatePool('fourWheeler', 'sharedSlots', value)} />
                        </div>
                        <p style={{ margin: '0.75rem 0 0.5rem', fontSize: '0.85rem', color: 'var(--color-text-muted)' }}>2-Wheeler (Bike / Scooter)</p>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '0.75rem' }}>
                            <Field label="2W Total" type="number" min="0" value={allocationForm.twoWheeler.totalSlots} onChange={value => updatePool('twoWheeler', 'totalSlots', value)} />
                            <Field label="2W Fixed" type="number" min="0" value={allocationForm.twoWheeler.fixedSlots} onChange={value => updatePool('twoWheeler', 'fixedSlots', value)} />
                            <Field label="2W Shared" type="number" min="0" value={allocationForm.twoWheeler.sharedSlots} onChange={value => updatePool('twoWheeler', 'sharedSlots', value)} />
                        </div>
                        <Field label="Monthly Rate" type="number" min="0" value={allocationForm.monthlyRate} onChange={value => setAllocationForm(prev => ({ ...prev, monthlyRate: value }))} />
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                            <Field label="Start Date" type="date" value={allocationForm.startDate} onChange={value => setAllocationForm(prev => ({ ...prev, startDate: value }))} required />
                            <Field label="End Date" type="date" value={allocationForm.endDate} onChange={value => setAllocationForm(prev => ({ ...prev, endDate: value }))} required />
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '1rem' }}>
                            <button type="button" className="btn btn-secondary" onClick={() => setAllocationForm(defaultAllocation)}>Cancel</button>
                            <button type="submit" className="btn btn-primary" disabled={allocating}>{allocating ? 'Activating...' : 'Activate Allocation'}</button>
                        </div>
                    </form>
                </div>
            )}
        </div>
    );
};

const Field = ({ label, value, onChange, type = 'text', required = false, min, step }) => (
    <label style={{ display: 'block', marginBottom: '0.85rem', color: 'var(--color-text-secondary)', fontSize: '0.85rem' }}>
        <span style={{ display: 'block', marginBottom: '0.35rem' }}>{label}</span>
        <input
            type={type}
            value={value}
            onChange={event => onChange(event.target.value)}
            required={required}
            min={min}
            step={step}
            style={{ width: '100%', padding: '10px', background: 'var(--color-bg-primary)', border: '1px solid var(--color-border)', borderRadius: '6px', color: 'var(--color-text-primary)' }}
        />
    </label>
);

export default CorporateParkingSpaces;
