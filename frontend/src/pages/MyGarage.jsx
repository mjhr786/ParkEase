import React, { useState, useEffect } from 'react';
import api from '../services/api';
import showToast from '../utils/toast.jsx';
import './MyGarage.css';

const VEHICLE_TYPES = [
    { value: 0, label: 'Car' },
    { value: 1, label: 'Motorcycle' },
    { value: 2, label: 'SUV' },
    { value: 3, label: 'Truck' },
    { value: 4, label: 'Van' },
    { value: 5, label: 'Electric' }
];

const initialFormState = {
    licensePlate: '',
    make: '',
    model: '',
    color: '',
    type: 0,
    isDefault: false
};

const MyGarage = () => {
    const [vehicles, setVehicles] = useState([]);
    const [loading, setLoading] = useState(true);
    const [isFormOpen, setIsFormOpen] = useState(false);
    const [editingVehicleId, setEditingVehicleId] = useState(null);
    const [formData, setFormData] = useState(initialFormState);
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => {
        fetchVehicles();
    }, []);

    const fetchVehicles = async () => {
        try {
            setLoading(true);
            const data = await api.getMyVehicles();
            setVehicles(data || []);
        } catch (error) {
            showToast.error('Failed to load vehicles');
        } finally {
            setLoading(false);
        }
    };

    const handleInputChange = (e) => {
        const { name, value, type, checked } = e.target;
        setFormData(prev => ({
            ...prev,
            [name]: type === 'checkbox' ? checked : value
        }));
    };

    const handleOpenForm = (vehicle = null) => {
        if (vehicle) {
            setFormData({
                licensePlate: vehicle.licensePlate,
                make: vehicle.make,
                model: vehicle.model,
                color: vehicle.color,
                type: vehicle.type,
                isDefault: vehicle.isDefault
            });
            setEditingVehicleId(vehicle.id);
        } else {
            setFormData(initialFormState);
            setEditingVehicleId(null);
        }
        setIsFormOpen(true);
    };

    const handleCloseForm = () => {
        setIsFormOpen(false);
        setFormData(initialFormState);
        setEditingVehicleId(null);
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        if (!formData.licensePlate || !formData.make || !formData.model || !formData.color) {
            showToast.error('Please fill in all required fields');
            return;
        }

        // Check for duplicate license plate in frontend
        const normalizedPlate = formData.licensePlate.trim().toUpperCase();
        const isDuplicate = vehicles.some(v =>
            v.licensePlate.toUpperCase() === normalizedPlate && v.id !== editingVehicleId
        );

        if (isDuplicate) {
            showToast.error('A vehicle with this license plate already exists');
            return;
        }

        try {
            setSubmitting(true);
            const payload = {
                ...formData,
                licensePlate: normalizedPlate,
                type: parseInt(formData.type)
            };

            if (editingVehicleId) {
                await api.updateVehicle(editingVehicleId, payload);
                showToast.success('Vehicle updated successfully');
            } else {
                await api.addVehicle(payload);
                showToast.success('Vehicle added successfully');
            }

            await fetchVehicles();
            handleCloseForm();
        } catch (error) {
            showToast.error(error.message || 'Failed to save vehicle');
        } finally {
            setSubmitting(false);
        }
    };

    const handleDelete = async (id) => {
        if (!window.confirm('Are you sure you want to remove this vehicle?')) return;

        try {
            await api.deleteVehicle(id);
            showToast.success('Vehicle removed');
            await fetchVehicles();
        } catch (error) {
            showToast.error(error.message || 'Failed to remove vehicle');
        }
    };

    const getVehicleTypeName = (typeValue) => {
        const type = VEHICLE_TYPES.find(t => t.value === typeValue);
        return type ? type.label : 'Unknown';
    };

    if (loading) {
        return <div className="loading-spinner">Loading...</div>;
    }

    return (
        <div className="my-garage-container">
            <div className="my-garage-header">
                <h2>My Garage</h2>
                <button
                    className="btn btn-primary"
                    onClick={() => handleOpenForm()}
                    disabled={isFormOpen}
                >
                    + Add Vehicle
                </button>
            </div>

            {isFormOpen && (
                <div className="card my-garage-form-card mb-4 fade-in">
                    <div className="card-header d-flex justify-content-between align-items-center">
                        <h3 className="m-0">{editingVehicleId ? 'Edit Vehicle' : 'Add New Vehicle'}</h3>
                        <button className="btn btn-outline" style={{ padding: '0.25rem 0.5rem' }} onClick={handleCloseForm}>&times;</button>
                    </div>
                    <div className="card-body">
                        <form onSubmit={handleSubmit} className="vehicle-form">
                            <div className="grid grid-2">
                                <div className="form-group mb-0">
                                    <label className="form-label">License Plate *</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        name="licensePlate"
                                        value={formData.licensePlate}
                                        onChange={handleInputChange}
                                        placeholder="e.g. MH01AB1234"
                                        required
                                    />
                                </div>

                                <div className="form-group mb-0">
                                    <label className="form-label">Make *</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        name="make"
                                        value={formData.make}
                                        onChange={handleInputChange}
                                        placeholder="e.g. Toyota, Honda, Ford"
                                        required
                                    />
                                </div>

                                <div className="form-group mb-0">
                                    <label className="form-label">Model *</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        name="model"
                                        value={formData.model}
                                        onChange={handleInputChange}
                                        placeholder="e.g. Corolla, Civic, Mustang"
                                        required
                                    />
                                </div>

                                <div className="form-group mb-0">
                                    <label className="form-label">Color *</label>
                                    <input
                                        type="text"
                                        className="form-input"
                                        name="color"
                                        value={formData.color}
                                        onChange={handleInputChange}
                                        placeholder="e.g. Red, Blue, Silver"
                                        required
                                    />
                                </div>

                                <div className="form-group mb-0">
                                    <label className="form-label">Vehicle Type</label>
                                    <select
                                        className="form-select"
                                        name="type"
                                        value={formData.type}
                                        onChange={handleInputChange}
                                    >
                                        {VEHICLE_TYPES.map(type => (
                                            <option key={type.value} value={type.value}>{type.label}</option>
                                        ))}
                                    </select>
                                </div>

                                <div className="form-group mb-0 flex gap-2" style={{ alignItems: 'center' }}>
                                    <input
                                        type="checkbox"
                                        id="isDefault"
                                        name="isDefault"
                                        checked={formData.isDefault}
                                        onChange={handleInputChange}
                                        style={{ width: '1.2rem', height: '1.2rem', cursor: 'pointer' }}
                                    />
                                    <label className="form-label m-0" htmlFor="isDefault" style={{ cursor: 'pointer', marginBottom: 0 }}>
                                        Set as default vehicle
                                    </label>
                                </div>
                            </div>

                            <div className="flex gap-2 mt-4">
                                <button
                                    type="button"
                                    className="btn btn-outline"
                                    onClick={handleCloseForm}
                                    disabled={submitting}
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    className="btn btn-primary"
                                    disabled={submitting}
                                >
                                    {submitting ? 'Saving...' : 'Save Vehicle'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {!isFormOpen && vehicles.length === 0 && (
                <div className="empty-state">
                    <div className="empty-state-icon">🚗</div>
                    <h3>No Vehicles Found</h3>
                    <p>Add your vehicles to make booking parking spots faster.</p>
                </div>
            )}

            <div className="vehicles-grid">
                {vehicles.map(vehicle => (
                    <div className={`card hover-card vehicle-card ${vehicle.isDefault ? 'border-primary' : ''}`} key={vehicle.id}>
                        <div className="card-body">
                            <div className="flex-between align-items-start mb-3">
                                <div>
                                    <h4 className="m-0 vehicle-title d-flex align-items-center gap-2">
                                        🚘 {vehicle.make} {vehicle.model}
                                    </h4>
                                    <span className="badge badge-secondary mt-1">
                                        {getVehicleTypeName(vehicle.type)}
                                    </span>
                                </div>
                                {vehicle.isDefault && (
                                    <span className="badge badge-success">Default</span>
                                )}
                            </div>

                            <div className="vehicle-details">
                                <div className="detail-item">
                                    <span className="text-muted text-sm">License Plate</span>
                                    <span className="fw-bold vehicle-plate px-2 py-1 rounded">{vehicle.licensePlate}</span>
                                </div>
                                <div className="detail-item">
                                    <span className="text-muted text-sm">Color</span>
                                    <span className="vehicle-color-tag">{vehicle.color}</span>
                                </div>
                            </div>

                            <div className="vehicle-actions">
                                <button
                                    className="btn btn-secondary flex-1"
                                    onClick={() => handleOpenForm(vehicle)}
                                    style={{ padding: '0.5rem' }}
                                >
                                    ✏️ Edit
                                </button>
                                <button
                                    className="btn btn-outline flex-1"
                                    onClick={() => handleDelete(vehicle.id)}
                                    style={{ padding: '0.5rem', borderColor: 'var(--color-danger)', color: 'var(--color-danger)' }}
                                >
                                    🗑️ Remove
                                </button>
                            </div>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};

export default MyGarage;
