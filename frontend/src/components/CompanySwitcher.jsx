import React, { useState, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { useCompany } from '../contexts/CompanyContext';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import corporateService from '../services/corporateService';
import showToast from '../utils/toast.jsx';

/**
 * Corporate company switcher only.
 * Marketplace and Corporate are separate products/accounts — no cross-channel CTA or exit.
 * Within Corporate: switch companies via POST /auth/channel.
 */
const CompanySwitcher = () => {
    const { activeCompanyId, companyDetails, isCorporateMode, switchCompany } = useCompany();
    const {
        isAuthenticated,
        applySession,
        channel,
        switchChannel,
        companyId: jwtCompanyId,
    } = useAuth();
    const navigate = useNavigate();
    const [isOpen, setIsOpen] = useState(false);
    const [myCompanies, setMyCompanies] = useState([]);
    const [loading, setLoading] = useState(false);
    const [switching, setSwitching] = useState(false);

    const [showCreateModal, setShowCreateModal] = useState(false);
    const [createLoading, setCreateLoading] = useState(false);
    const [formData, setFormData] = useState({
        name: '',
        registrationNumber: '',
        contactEmail: '',
        contactPhone: '',
        billingAddress: '',
        billingType: 0,
    });

    const dropdownRef = useRef(null);

    useEffect(() => {
        const handleClickOutside = (event) => {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
                setIsOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    useEffect(() => {
        if (isOpen && isAuthenticated && myCompanies.length === 0) {
            fetchMyCompanies();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
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
            console.error('Failed to load companies', error);
        } finally {
            setLoading(false);
        }
    };

    const handleSwitchCompany = async (companyId) => {
        setSwitching(true);
        try {
            const result = await switchChannel({
                channel: 'Corporate',
                companyId,
            });
            setIsOpen(false);
            if (result.success) {
                switchCompany(companyId);
                showToast.success('Switched company workspace');
                navigate('/corporate/dashboard', { replace: true });
            } else {
                showToast.error(result.message || 'Could not switch company');
            }
        } catch (error) {
            console.error('switchChannel failed', error);
            showToast.error('Could not switch company');
        } finally {
            setSwitching(false);
        }
    };

    const handleCreateCompany = async (e) => {
        e.preventDefault();
        setCreateLoading(true);
        try {
            const res = await corporateService.createCompany(formData);
            if (res.success) {
                const company = res.data?.company || res.data;
                const session = res.data?.session;
                const newId = company?.id || res.data?.id;

                if (session?.accessToken && session?.refreshToken && applySession) {
                    applySession(session);
                } else if (newId && switchChannel) {
                    await switchChannel({ channel: 'Corporate', companyId: newId });
                }

                showToast.success('Corporate account created successfully!');
                setShowCreateModal(false);
                setFormData({
                    name: '',
                    registrationNumber: '',
                    contactEmail: '',
                    contactPhone: '',
                    billingAddress: '',
                    billingType: 0,
                });
                fetchMyCompanies();
                if (newId) {
                    switchCompany(newId);
                    navigate('/corporate/dashboard', { replace: true });
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

    // Only show on Corporate product sessions — marketplace has no corporate entry point
    if (!isAuthenticated || channel !== 'Corporate') return null;

    const effectiveCompanyId = jwtCompanyId || activeCompanyId;
    const currentName =
        isCorporateMode && companyDetails
            ? companyDetails.name
            : 'Corporate workspace';

    return (
        <div ref={dropdownRef} style={{ position: 'relative', display: 'inline-block' }}>
            <button
                onClick={() => setIsOpen(!isOpen)}
                style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '8px 16px',
                    background:
                        'linear-gradient(135deg, var(--color-success) 0%, color-mix(in srgb, var(--color-success) 80%, black) 100%)',
                    color: 'white',
                    border: '1px solid var(--control-border)',
                    borderRadius: '8px',
                    cursor: 'pointer',
                    fontSize: '0.9rem',
                    fontWeight: '600',
                    transition: 'all 0.2s',
                    boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)',
                }}
            >
                <span role="img" aria-label="building" style={{ fontSize: '1.1rem' }}>
                    🏢
                </span>
                {currentName}
                <svg
                    width="12"
                    height="12"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    style={{ transform: isOpen ? 'rotate(180deg)' : 'rotate(0)' }}
                >
                    <polyline points="6 9 12 15 18 9"></polyline>
                </svg>
            </button>

            {isOpen && (
                <div
                    style={{
                        position: 'absolute',
                        top: '100%',
                        right: 0,
                        marginTop: '8px',
                        background: 'var(--dropdown-bg)',
                        border: '1px solid var(--dropdown-border)',
                        borderRadius: '8px',
                        boxShadow: 'var(--shadow-dropdown)',
                        minWidth: '240px',
                        zIndex: 9999,
                        overflow: 'hidden',
                    }}
                >
                    <div style={{ padding: '8px', borderBottom: '1px solid var(--dropdown-border)' }}>
                        <span
                            style={{
                                fontSize: '0.8rem',
                                color: 'var(--dropdown-muted)',
                                textTransform: 'uppercase',
                                letterSpacing: '0.05em',
                            }}
                        >
                            Company workspace
                        </span>
                    </div>

                    {loading || switching ? (
                        <div
                            style={{
                                padding: '12px',
                                textAlign: 'center',
                                color: 'var(--dropdown-muted)',
                                fontSize: '0.8rem',
                            }}
                        >
                            {switching ? 'Switching…' : 'Loading...'}
                        </div>
                    ) : (
                        myCompanies.map((company) => (
                            <button
                                key={company.id}
                                onClick={() => handleSwitchCompany(company.id)}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    width: '100%',
                                    padding: '12px 16px',
                                    background:
                                        String(effectiveCompanyId) === String(company.id)
                                            ? 'rgba(16, 185, 129, 0.1)'
                                            : 'transparent',
                                    border: 'none',
                                    color:
                                        String(effectiveCompanyId) === String(company.id)
                                            ? 'var(--color-success)'
                                            : 'var(--color-text-primary)',
                                    textAlign: 'left',
                                    cursor: 'pointer',
                                    fontSize: '0.9rem',
                                    borderTop: '1px solid var(--dropdown-border)',
                                }}
                            >
                                <span style={{ marginRight: '8px' }}>🏢</span>{' '}
                                {company.name || 'Corporate Account'}
                            </button>
                        ))
                    )}

                    {!loading && !switching && myCompanies.length === 0 && (
                        <div
                            style={{
                                padding: '12px 16px',
                                color: 'var(--dropdown-muted)',
                                fontSize: '0.85rem',
                                fontStyle: 'italic',
                                borderTop: '1px solid var(--dropdown-border)',
                            }}
                        >
                            No corporate accounts found.
                        </div>
                    )}

                    <button
                        onClick={() => {
                            setShowCreateModal(true);
                            setIsOpen(false);
                        }}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            width: '100%',
                            padding: '12px 16px',
                            background: 'var(--color-primary-alpha)',
                            border: 'none',
                            color: 'var(--color-accent-light)',
                            textAlign: 'left',
                            cursor: 'pointer',
                            fontSize: '0.9rem',
                            borderTop: '1px solid var(--dropdown-border)',
                        }}
                    >
                        <span style={{ marginRight: '8px', fontSize: '1.2rem' }}>+</span> Create
                        Corporate Account
                    </button>
                </div>
            )}

            {showCreateModal &&
                createPortal(
                    <div
                        style={{
                            position: 'fixed',
                            top: 0,
                            left: 0,
                            right: 0,
                            bottom: 0,
                            background: 'var(--overlay-bg)',
                            backdropFilter: 'blur(4px)',
                            display: 'flex',
                            alignItems: 'flex-start',
                            justifyContent: 'center',
                            zIndex: 100000,
                            overflowY: 'auto',
                            padding: '40px 16px',
                        }}
                        onClick={(e) => {
                            if (e.target === e.currentTarget) setShowCreateModal(false);
                        }}
                    >
                        <div
                            style={{
                                background: 'var(--dropdown-bg)',
                                border: '1px solid var(--dropdown-border)',
                                borderRadius: '12px',
                                padding: '24px',
                                width: '100%',
                                maxWidth: '500px',
                                boxShadow: 'var(--shadow-dropdown)',
                                margin: 'auto 0',
                            }}
                        >
                            <h2
                                style={{
                                    margin: '0 0 16px 0',
                                    fontSize: '1.25rem',
                                    color: 'var(--color-text-primary)',
                                }}
                            >
                                Create Corporate Account
                            </h2>

                            <form onSubmit={handleCreateCompany}>
                                <div className="grid grid-2" style={{ gap: '12px', marginBottom: '12px' }}>
                                    <div className="form-group" style={{ margin: 0 }}>
                                        <label className="form-label">Company Name *</label>
                                        <input
                                            type="text"
                                            className="form-input"
                                            required
                                            value={formData.name}
                                            onChange={(e) =>
                                                setFormData({ ...formData, name: e.target.value })
                                            }
                                        />
                                    </div>
                                    <div className="form-group" style={{ margin: 0 }}>
                                        <label className="form-label">Registration No. *</label>
                                        <input
                                            type="text"
                                            className="form-input"
                                            required
                                            value={formData.registrationNumber}
                                            onChange={(e) =>
                                                setFormData({
                                                    ...formData,
                                                    registrationNumber: e.target.value,
                                                })
                                            }
                                        />
                                    </div>
                                    <div className="form-group" style={{ margin: 0 }}>
                                        <label className="form-label">Contact Email *</label>
                                        <input
                                            type="email"
                                            className="form-input"
                                            required
                                            value={formData.contactEmail}
                                            onChange={(e) =>
                                                setFormData({
                                                    ...formData,
                                                    contactEmail: e.target.value,
                                                })
                                            }
                                        />
                                    </div>
                                    <div className="form-group" style={{ margin: 0 }}>
                                        <label className="form-label">Contact Phone *</label>
                                        <input
                                            type="text"
                                            className="form-input"
                                            required
                                            value={formData.contactPhone}
                                            onChange={(e) =>
                                                setFormData({
                                                    ...formData,
                                                    contactPhone: e.target.value,
                                                })
                                            }
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
                                        onChange={(e) =>
                                            setFormData({
                                                ...formData,
                                                billingAddress: e.target.value,
                                            })
                                        }
                                    ></textarea>
                                </div>

                                <div className="form-group">
                                    <label className="form-label">Billing Type</label>
                                    <select
                                        className="form-select"
                                        value={formData.billingType}
                                        onChange={(e) =>
                                            setFormData({
                                                ...formData,
                                                billingType: parseInt(e.target.value),
                                            })
                                        }
                                    >
                                        <option value={0}>Reserved slots</option>
                                        <option value={1}>Usage based</option>
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
                    </div>,
                    document.body
                )}
        </div>
    );
};

export default CompanySwitcher;
