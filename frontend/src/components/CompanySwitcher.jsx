import React, { useState, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { useCompany } from '../contexts/CompanyContext';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import api from '../services/api';
import corporateService from '../services/corporateService';
import showToast from '../utils/toast.jsx';

const CompanySwitcher = () => {
    const { activeCompanyId, companyDetails, isCorporateMode, switchCompany } = useCompany();
    const { isAuthenticated } = useAuth();
    const navigate = useNavigate();
    const [isOpen, setIsOpen] = useState(false);
    const [myCompanies, setMyCompanies] = useState([]);
    const [loading, setLoading] = useState(false);
    
    // Create Company Modal State
    const [showCreateModal, setShowCreateModal] = useState(false);
    const [createLoading, setCreateLoading] = useState(false);
    const [formData, setFormData] = useState({
        name: '',
        registrationNumber: '',
        contactEmail: '',
        contactPhone: '',
        billingAddress: '',
        billingType: 0 // 0 = Invoice, 1 = CreditCard
    });

    const dropdownRef = useRef(null);

    // Close on click outside
    useEffect(() => {
        const handleClickOutside = (event) => {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
                setIsOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    // Fetch user profile companies when dropdown opens
    useEffect(() => {
        if (isOpen && isAuthenticated && myCompanies.length === 0) {
            fetchMyCompanies();
        }
    }, [isOpen]);

    const fetchMyCompanies = async () => {
        setLoading(true);
        try {
            const res = await corporateService.getMyCompanies();
            if (res.success && res.data) {
                setMyCompanies(res.data);
            } else {
                setMyCompanies([]); 
            }
        } catch (error) {
            console.error("Failed to load companies", error);
        } finally {
            setLoading(false);
        }
    };

    const handleSwitchToPersonal = () => {
        setIsOpen(false);
        navigate('/dashboard', { replace: true });
        switchCompany(null);
    };

    const handleSwitchToCorporate = (companyId) => {
        setIsOpen(false);
        navigate('/corporate/dashboard', { replace: true });
        switchCompany(companyId);
    };

    const handleCreateCompany = async (e) => {
        e.preventDefault();
        setCreateLoading(true);
        try {
            const res = await corporateService.createCompany(formData);
            if (res.success) {
                showToast.success('Corporate account created successfully!');
                setShowCreateModal(false);
                setFormData({
                    name: '',
                    registrationNumber: '',
                    contactEmail: '',
                    contactPhone: '',
                    billingAddress: '',
                    billingType: 0
                });
                // Switch to the newly created company if IDs are returned, otherwise just refetch
                fetchMyCompanies();
                if (res.data && res.data.id) {
                    handleSwitchToCorporate(res.data.id);
                }
            } else {
                showToast.error(res.message || 'Failed to create corporate account');
            }
        } catch (error) {
            console.error('Error creating company:', error);
            showToast.error('An error occurred while creating the corporate account.');
        } finally {
            setCreateLoading(false);
        }
    };

    if (!isAuthenticated) return null;

    const currentName = isCorporateMode && companyDetails ? companyDetails.name : 'Personal Mode';

    return (
        <div ref={dropdownRef} style={{ position: 'relative', display: 'inline-block' }}>
            <button
                onClick={() => setIsOpen(!isOpen)}
                style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '8px 16px',
                    background: isCorporateMode ? 'linear-gradient(135deg, #10b981 0%, #059669 100%)' : 'rgba(255, 255, 255, 0.1)',
                    color: 'white',
                    border: '1px solid rgba(255,255,255,0.2)',
                    borderRadius: '8px',
                    cursor: 'pointer',
                    fontSize: '0.9rem',
                    fontWeight: '600',
                    transition: 'all 0.2s',
                    boxShadow: isCorporateMode ? '0 4px 12px rgba(16, 185, 129, 0.3)' : 'none'
                }}
            >
                <span role="img" aria-label="building" style={{ fontSize: '1.1rem' }}>
                    {isCorporateMode ? '🏢' : '👤'}
                </span>
                {currentName}
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ transform: isOpen ? 'rotate(180deg)' : 'rotate(0)' }}>
                    <polyline points="6 9 12 15 18 9"></polyline>
                </svg>
            </button>

            {isOpen && (
                <div style={{
                    position: 'absolute',
                    top: '100%',
                    right: 0,
                    marginTop: '8px',
                    background: '#1e293b',
                    border: '1px solid rgba(255,255,255,0.1)',
                    borderRadius: '8px',
                    boxShadow: '0 10px 25px rgba(0,0,0,0.5)',
                    minWidth: '220px',
                    zIndex: 9999,
                    overflow: 'hidden'
                }}>
                    <div style={{ padding: '8px', borderBottom: '1px solid rgba(255,255,255,0.1)' }}>
                        <span style={{ fontSize: '0.8rem', color: '#94a3b8', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Switch Account Context</span>
                    </div>
                    
                    <button
                        onClick={handleSwitchToPersonal}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            width: '100%',
                            padding: '12px 16px',
                            background: !isCorporateMode ? 'rgba(255,255,255,0.05)' : 'transparent',
                            border: 'none',
                            color: 'white',
                            textAlign: 'left',
                            cursor: 'pointer',
                            fontSize: '0.9rem'
                        }}
                    >
                        <span style={{ marginRight: '8px' }}>👤</span> Personal Mode
                    </button>

                    {loading ? (
                        <div style={{ padding: '12px', textAlign: 'center', color: '#94a3b8', fontSize: '0.8rem' }}>Loading...</div>
                    ) : (
                        myCompanies.map((company) => (
                            <button
                                key={company.id}
                                onClick={() => handleSwitchToCorporate(company.id)}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    width: '100%',
                                    padding: '12px 16px',
                                    background: activeCompanyId === company.id ? 'rgba(16, 185, 129, 0.1)' : 'transparent',
                                    border: 'none',
                                    color: activeCompanyId === company.id ? '#10b981' : 'white',
                                    textAlign: 'left',
                                    cursor: 'pointer',
                                    fontSize: '0.9rem',
                                    borderTop: '1px solid rgba(255,255,255,0.05)'
                                }}
                            >
                                <span style={{ marginRight: '8px' }}>🏢</span> {company.name || 'Corporate Account'}
                            </button>
                        ))
                    )}
                    
                    {!loading && myCompanies.length === 0 && (
                        <div style={{ padding: '12px 16px', color: '#94a3b8', fontSize: '0.85rem', fontStyle: 'italic', borderTop: '1px solid rgba(255,255,255,0.05)' }}>
                            No corporate accounts found.
                        </div>
                    )}
                    
                    <button
                        onClick={() => { setShowCreateModal(true); setIsOpen(false); }}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            width: '100%',
                            padding: '12px 16px',
                            background: 'rgba(99, 102, 241, 0.1)',
                            border: 'none',
                            color: '#818cf8',
                            textAlign: 'left',
                            cursor: 'pointer',
                            fontSize: '0.9rem',
                            borderTop: '1px solid rgba(255,255,255,0.05)'
                        }}
                    >
                        <span style={{ marginRight: '8px', fontSize: '1.2rem' }}>+</span> Create Corporate Account
                    </button>
                </div>
            )}

            {/* Create Company Modal — rendered via portal to escape header stacking context */}
            {showCreateModal && createPortal(
                <div style={{
                    position: 'fixed',
                    top: 0, left: 0, right: 0, bottom: 0,
                    background: 'rgba(15, 23, 42, 0.8)',
                    backdropFilter: 'blur(4px)',
                    display: 'flex',
                    alignItems: 'flex-start',
                    justifyContent: 'center',
                    zIndex: 100000,
                    overflowY: 'auto',
                    padding: '40px 16px'
                }}
                    onClick={(e) => { if (e.target === e.currentTarget) setShowCreateModal(false); }}
                >
                    <div style={{
                        background: '#1e293b',
                        border: '1px solid rgba(255,255,255,0.1)',
                        borderRadius: '12px',
                        padding: '24px',
                        width: '100%',
                        maxWidth: '500px',
                        boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.5)',
                        margin: 'auto 0'
                    }}>
                        <h2 style={{ margin: '0 0 16px 0', fontSize: '1.25rem', color: 'white' }}>Create Corporate Account</h2>
                        
                        <form onSubmit={handleCreateCompany}>
                            <div className="grid grid-2" style={{ gap: '12px', marginBottom: '12px' }}>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Company Name *</label>
                                    <input 
                                        type="text" 
                                        className="form-input" 
                                        required 
                                        value={formData.name}
                                        onChange={e => setFormData({...formData, name: e.target.value})}
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Registration No. *</label>
                                    <input 
                                        type="text" 
                                        className="form-input" 
                                        required 
                                        value={formData.registrationNumber}
                                        onChange={e => setFormData({...formData, registrationNumber: e.target.value})}
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Contact Email *</label>
                                    <input 
                                        type="email" 
                                        className="form-input" 
                                        required 
                                        value={formData.contactEmail}
                                        onChange={e => setFormData({...formData, contactEmail: e.target.value})}
                                    />
                                </div>
                                <div className="form-group" style={{ margin: 0 }}>
                                    <label className="form-label">Contact Phone *</label>
                                    <input 
                                        type="text" 
                                        className="form-input" 
                                        required 
                                        value={formData.contactPhone}
                                        onChange={e => setFormData({...formData, contactPhone: e.target.value})}
                                    />
                                </div>
                            </div>
                            
                            <div className="form-group">
                                <label className="form-label">Billing Address *</label>
                                <textarea 
                                    className="form-input" 
                                    required 
                                    rows="2"
                                    value={formData.billingAddress}
                                    onChange={e => setFormData({...formData, billingAddress: e.target.value})}
                                ></textarea>
                            </div>
                            
                            <div className="form-group">
                                <label className="form-label">Billing Type</label>
                                <select 
                                    className="form-select"
                                    value={formData.billingType}
                                    onChange={e => setFormData({...formData, billingType: parseInt(e.target.value)})}
                                >
                                    <option value={0}>Invoice</option>
                                    <option value={1}>Credit Card</option>
                                </select>
                            </div>

                            <div style={{ display: 'flex', gap: '12px', marginTop: '24px' }}>
                                <button 
                                    type="button" 
                                    className="btn btn-secondary" 
                                    style={{ flex: 1 }}
                                    onClick={() => setShowCreateModal(false)}
                                >
                                    Cancel
                                </button>
                                <button 
                                    type="submit" 
                                    className="btn btn-primary" 
                                    style={{ flex: 2 }}
                                    disabled={createLoading}
                                >
                                    {createLoading ? 'Creating...' : 'Create Account'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            , document.body)}
        </div>
    );
};

export default CompanySwitcher;
