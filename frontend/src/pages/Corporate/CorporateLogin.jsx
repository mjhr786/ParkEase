import { useState, useEffect } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import showToast from '../../utils/toast.jsx';
import { safeReturnUrl } from '../../utils/safeReturnUrl';

/**
 * Corporate product entry (PR6 / KD-3 / KD-16).
 * Supports bootstrap (zero memberships), single/multi company bind, and ?companyId= preselect.
 */
export default function CorporateLogin() {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [loading, setLoading] = useState(false);
    const [memberships, setMemberships] = useState(null);
    const { loginCorporate, isAuthenticated, channel, isBootstrap } = useAuth();
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const returnUrl = safeReturnUrl(searchParams.get('returnUrl'));
    const preselectedCompanyId = searchParams.get('companyId') || null;

    const finishSuccess = (result) => {
        if (result.isBootstrap) {
            navigate(returnUrl || '/corporate/create-company');
            return;
        }
        if (returnUrl) {
            navigate(returnUrl);
            return;
        }
        navigate('/corporate/dashboard');
    };

    // Already in corporate product — bounce away from login
    useEffect(() => {
        if (!isAuthenticated || channel !== 'Corporate' || memberships) return;
        if (isBootstrap) {
            navigate(returnUrl || '/corporate/create-company', { replace: true });
        } else {
            navigate(returnUrl || '/corporate/dashboard', { replace: true });
        }
    }, [isAuthenticated, channel, isBootstrap, memberships, navigate, returnUrl]);

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);
        setMemberships(null);

        const result = await loginCorporate(email, password, preselectedCompanyId);

        if (result.requiresCompanySelection && result.memberships?.length) {
            setMemberships(result.memberships);
            setLoading(false);
            return;
        }

        if (result.success) {
            showToast.success(result.isBootstrap ? 'Welcome — create your company' : 'Signed in to Corporate');
            finishSuccess(result);
        } else {
            showToast.error(result.message || 'Corporate login failed');
        }
        setLoading(false);
    };

    const handleSelectCompany = async (companyId) => {
        setLoading(true);
        const result = await loginCorporate(email, password, companyId);
        if (result.success) {
            showToast.success('Signed in to Corporate');
            finishSuccess(result);
        } else {
            showToast.error(result.message || 'Could not bind company');
        }
        setLoading(false);
    };

    return (
        <div className="auth-page">
            <div className="card auth-card">
                <h1 className="auth-title">Corporate Workspace</h1>
                <p className="auth-subtitle">
                    {preselectedCompanyId
                        ? 'Sign in to join your company workspace'
                        : 'Sign in to manage company parking, members, and allocations'}
                </p>

                {memberships ? (
                    <div>
                        <p style={{ marginBottom: '1rem', color: 'var(--color-text-secondary)' }}>
                            Select a company to continue:
                        </p>
                        <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
                            {memberships.map((m) => (
                                <li key={m.companyId || m.CompanyId} style={{ marginBottom: '0.5rem' }}>
                                    <button
                                        type="button"
                                        className="btn btn-secondary btn-full"
                                        disabled={loading}
                                        onClick={() =>
                                            handleSelectCompany(m.companyId || m.CompanyId)
                                        }
                                    >
                                        {m.companyName || m.CompanyName || 'Company'}
                                        {(m.role || m.Role) ? ` · ${m.role || m.Role}` : ''}
                                    </button>
                                </li>
                            ))}
                        </ul>
                        <button
                            type="button"
                            className="btn btn-link"
                            style={{ marginTop: '1rem' }}
                            onClick={() => setMemberships(null)}
                        >
                            Back
                        </button>
                    </div>
                ) : (
                    <form onSubmit={handleSubmit}>
                        <div className="form-group">
                            <label className="form-label">Email</label>
                            <input
                                type="email"
                                className="form-input"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                placeholder="Work email"
                                required
                                autoComplete="username"
                            />
                        </div>

                        <div className="form-group">
                            <label className="form-label">Password</label>
                            <input
                                type="password"
                                className="form-input"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                placeholder="Password"
                                required
                                autoComplete="current-password"
                            />
                        </div>

                        <button type="submit" className="btn btn-primary btn-full" disabled={loading}>
                            {loading ? 'Signing in...' : 'Sign in to Corporate'}
                        </button>
                    </form>
                )}

                <p className="auth-footer">
                    Personal / marketplace account?{' '}
                    <Link to={returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/login'}>
                        Marketplace login
                    </Link>
                </p>
            </div>
        </div>
    );
}
