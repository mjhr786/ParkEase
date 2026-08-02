import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import AuthChannelSelector, {
    authPath,
    channelFromSearchParams,
} from '../components/AuthChannelSelector';
import showToast from '../utils/toast.jsx';
import { postAuthDestination, safeReturnUrl } from '../utils/safeReturnUrl';

export default function Login() {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [loading, setLoading] = useState(false);
    const [memberships, setMemberships] = useState(null);
    const { login, loginCorporate, isAdmin } = useAuth();
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    const returnUrl = safeReturnUrl(searchParams.get('returnUrl'));
    const channel = channelFromSearchParams(searchParams);
    const isCorporate = channel === 'corporate';
    const preselectedCompanyId = searchParams.get('companyId') || null;

    const setChannel = (next) => {
        setMemberships(null);
        const nextParams = new URLSearchParams(searchParams);
        if (next === 'corporate') {
            nextParams.set('channel', 'corporate');
        } else {
            nextParams.delete('channel');
        }
        setSearchParams(nextParams, { replace: true });
    };

    const finishMarketplace = () => {
        const stored = localStorage.getItem('user');
        let admin = false;
        try {
            const u = stored ? JSON.parse(stored) : null;
            admin = u?.role === 0 || u?.role === 'Admin';
        } catch {
            /* ignore */
        }
        // Marketplace sign-in → marketplace dashboard (or safe same-channel returnUrl / admin)
        navigate(
            postAuthDestination('marketplace', {
                returnUrl,
                isAdmin: admin || isAdmin,
            }),
        );
    };

    const finishCorporate = (result) => {
        // Corporate sign-in → corporate dashboard (or create-company when bootstrap)
        navigate(
            postAuthDestination('corporate', {
                returnUrl,
                isBootstrap: !!result?.isBootstrap,
            }),
        );
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);
        setMemberships(null);

        if (isCorporate) {
            const result = await loginCorporate(email, password, preselectedCompanyId);
            if (result.requiresCompanySelection && result.memberships?.length) {
                setMemberships(result.memberships);
                setLoading(false);
                return;
            }
            if (result.success) {
                showToast.success(
                    result.isBootstrap ? 'Welcome — create your company' : 'Signed in to Corporate',
                );
                finishCorporate(result);
            } else {
                showToast.error(result.message || 'Corporate login failed');
            }
            setLoading(false);
            return;
        }

        const result = await login(email, password);
        if (result.success) {
            finishMarketplace();
        } else {
            showToast.error(result.message || 'Login failed');
        }
        setLoading(false);
    };

    const handleSelectCompany = async (companyId) => {
        setLoading(true);
        const result = await loginCorporate(email, password, companyId);
        if (result.success) {
            showToast.success('Signed in to Corporate');
            finishCorporate(result);
        } else {
            showToast.error(result.message || 'Could not bind company');
        }
        setLoading(false);
    };

    const registerLink = authPath('/register', { channel, returnUrl });

    const subtitle = (() => {
        if (returnUrl?.includes('/invite/accept/')) {
            return 'Sign in to accept your company invitation';
        }
        if (isCorporate) {
            return preselectedCompanyId
                ? 'Sign in to join your company workspace'
                : 'Sign in to manage company parking, members, and allocations';
        }
        return 'Sign in to your account';
    })();

    return (
        <div className="auth-page">
            <div className="card auth-card">
                <h1 className="auth-title">Welcome Back</h1>
                <p className="auth-subtitle">{subtitle}</p>

                <AuthChannelSelector value={channel} onChange={setChannel} />

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
                                        {m.role || m.Role ? ` · ${m.role || m.Role}` : ''}
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
                                placeholder={isCorporate ? 'Work email' : 'Enter your email'}
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
                                placeholder="Enter your password"
                                required
                                autoComplete="current-password"
                            />
                        </div>

                        <button type="submit" className="btn btn-primary btn-full" disabled={loading}>
                            {loading
                                ? 'Signing in...'
                                : isCorporate
                                  ? 'Sign in to Corporate'
                                  : 'Sign In'}
                        </button>
                    </form>
                )}

                <p className="auth-footer">
                    Don&apos;t have an account? <Link to={registerLink}>Sign up</Link>
                </p>
            </div>
        </div>
    );
}
