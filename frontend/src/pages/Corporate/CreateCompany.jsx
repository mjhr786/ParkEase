import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import corporateService from '../../services/corporateService';
import showToast from '../../utils/toast.jsx';

/**
 * Founder create-company handoff (PR6 / KD-16a).
 * On success: apply data.session TokenDto (or POST /auth/channel fallback) before dashboard.
 */
export default function CreateCompany() {
    const navigate = useNavigate();
    const { applySession, switchChannel, isAuthenticated, channel, isBootstrap } = useAuth();
    const [loading, setLoading] = useState(false);
    const [formData, setFormData] = useState({
        name: '',
        registrationNumber: '',
        contactEmail: '',
        contactPhone: '',
        billingAddress: '',
        billingType: 0,
    });
    const [pendingCompanyId, setPendingCompanyId] = useState(null);
    const [setupError, setSetupError] = useState(null);

    const resolveCompanyAndSession = (data) => {
        // CreateCompanyResultDto: { company, session } or legacy bare company
        const company = data?.company || data;
        const session = data?.session ?? null;
        const id = company?.id || company?.Id || data?.id;
        return { company, session, id: id ? String(id) : null };
    };

    const completeHandoff = async (companyId, session) => {
        if (session?.accessToken && session?.refreshToken) {
            applySession(session);
            return { success: true };
        }

        // Fallback: POST /api/auth/channel bootstrap → bound company
        const switchResult = await switchChannel({
            channel: 'Corporate',
            companyId,
        });
        if (switchResult.success) {
            return { success: true };
        }

        return {
            success: false,
            message: switchResult.message || 'Could not complete corporate session setup',
            code: switchResult.code,
        };
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);
        setSetupError(null);
        setPendingCompanyId(null);

        try {
            const res = await corporateService.createCompany(formData);
            if (!res.success) {
                showToast.error(res.message || 'Failed to create company');
                setLoading(false);
                return;
            }

            const { id, session } = resolveCompanyAndSession(res.data);
            if (!id) {
                showToast.error('Company created but response was incomplete');
                setLoading(false);
                return;
            }

            const handoff = await completeHandoff(id, session);
            if (!handoff.success) {
                setPendingCompanyId(id);
                setSetupError(handoff.message);
                showToast.error(handoff.message || 'Complete setup required');
                setLoading(false);
                return;
            }

            showToast.success('Company created — welcome to your workspace');
            navigate('/corporate/dashboard');
        } catch (error) {
            console.error('Create company error:', error);
            showToast.error(
                error.response?.data?.message || error.message || 'Failed to create company'
            );
        } finally {
            setLoading(false);
        }
    };

    const handleRetrySetup = async () => {
        if (!pendingCompanyId) return;
        setLoading(true);
        setSetupError(null);
        const handoff = await completeHandoff(pendingCompanyId, null);
        if (handoff.success) {
            showToast.success('Setup complete');
            navigate('/corporate/dashboard');
        } else {
            setSetupError(handoff.message);
            showToast.error(handoff.message || 'Setup failed');
        }
        setLoading(false);
    };

    if (!isAuthenticated) {
        return (
            <div className="auth-page">
                <div className="card auth-card">
                    <h1 className="auth-title">Create company</h1>
                    <p className="auth-subtitle">Sign in to Corporate first to create a company.</p>
                    <Link to="/corporate/login" className="btn btn-primary btn-full">
                        Corporate login
                    </Link>
                </div>
            </div>
        );
    }

    return (
        <div className="auth-page">
            <div className="card auth-card" style={{ maxWidth: 560 }}>
                <h1 className="auth-title">Create your company</h1>
                <p className="auth-subtitle">
                    {isBootstrap
                        ? 'Finish founder setup to unlock your corporate dashboard.'
                        : 'Register a new corporate account for parking management.'}
                </p>

                {setupError && (
                    <div
                        role="alert"
                        style={{
                            marginBottom: '1rem',
                            padding: '0.75rem 1rem',
                            borderRadius: 8,
                            background: 'rgba(239, 68, 68, 0.1)',
                            color: 'var(--color-error)',
                        }}
                    >
                        <p style={{ margin: '0 0 0.5rem' }}>{setupError}</p>
                        <button
                            type="button"
                            className="btn btn-secondary"
                            disabled={loading}
                            onClick={handleRetrySetup}
                        >
                            Complete setup
                        </button>
                    </div>
                )}

                <form onSubmit={handleSubmit}>
                    <div className="grid grid-2" style={{ gap: 12, marginBottom: 12 }}>
                        <div className="form-group" style={{ margin: 0 }}>
                            <label className="form-label" htmlFor="cc-name">Company Name *</label>
                            <input
                                id="cc-name"
                                type="text"
                                className="form-input"
                                required
                                value={formData.name}
                                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                            />
                        </div>
                        <div className="form-group" style={{ margin: 0 }}>
                            <label className="form-label" htmlFor="cc-reg">Registration No. *</label>
                            <input
                                id="cc-reg"
                                type="text"
                                className="form-input"
                                required
                                value={formData.registrationNumber}
                                onChange={(e) =>
                                    setFormData({ ...formData, registrationNumber: e.target.value })
                                }
                            />
                        </div>
                        <div className="form-group" style={{ margin: 0 }}>
                            <label className="form-label" htmlFor="cc-email">Contact Email *</label>
                            <input
                                id="cc-email"
                                type="email"
                                className="form-input"
                                required
                                value={formData.contactEmail}
                                onChange={(e) =>
                                    setFormData({ ...formData, contactEmail: e.target.value })
                                }
                            />
                        </div>
                        <div className="form-group" style={{ margin: 0 }}>
                            <label className="form-label" htmlFor="cc-phone">Contact Phone *</label>
                            <input
                                id="cc-phone"
                                type="text"
                                className="form-input"
                                required
                                value={formData.contactPhone}
                                onChange={(e) =>
                                    setFormData({ ...formData, contactPhone: e.target.value })
                                }
                            />
                        </div>
                    </div>

                    <div className="form-group">
                        <label className="form-label" htmlFor="cc-billing">Billing Address *</label>
                        <textarea
                            id="cc-billing"
                            className="form-input"
                            required
                            rows={2}
                            value={formData.billingAddress}
                            onChange={(e) =>
                                setFormData({ ...formData, billingAddress: e.target.value })
                            }
                        />
                    </div>

                    <div className="form-group">
                        <label className="form-label" htmlFor="cc-billing-type">Billing Type</label>
                        <select
                            id="cc-billing-type"
                            className="form-select"
                            value={formData.billingType}
                            onChange={(e) =>
                                setFormData({
                                    ...formData,
                                    billingType: parseInt(e.target.value, 10),
                                })
                            }
                        >
                            <option value={0}>Reserved Slots</option>
                            <option value={1}>Usage Based</option>
                        </select>
                    </div>

                    <button type="submit" className="btn btn-primary btn-full" disabled={loading}>
                        {loading ? 'Creating...' : 'Create company'}
                    </button>
                </form>
            </div>
        </div>
    );
}
