import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import AuthChannelSelector, {
    authPath,
    channelFromSearchParams,
} from '../components/AuthChannelSelector';
import showToast from '../utils/toast.jsx';
import { postAuthDestination, safeReturnUrl } from '../utils/safeReturnUrl';

export default function Register() {
    const [formData, setFormData] = useState({
        email: '',
        password: '',
        confirmPassword: '',
        firstName: '',
        lastName: '',
        phoneNumber: '',
    });
    const [loading, setLoading] = useState(false);
    const { register, switchChannel } = useAuth();
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    const returnUrl = safeReturnUrl(searchParams.get('returnUrl'));
    const channel = channelFromSearchParams(searchParams);
    const isCorporate = channel === 'corporate';

    const setChannel = (next) => {
        const nextParams = new URLSearchParams(searchParams);
        if (next === 'corporate') {
            nextParams.set('channel', 'corporate');
        } else {
            nextParams.delete('channel');
        }
        setSearchParams(nextParams, { replace: true });
    };

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        if (formData.password !== formData.confirmPassword) {
            showToast.error('Passwords do not match');
            return;
        }

        if (formData.password.length < 8) {
            showToast.error('Password must be at least 8 characters');
            return;
        }

        setLoading(true);

        const { confirmPassword, ...registerData } = formData;
        const result = await register(registerData);

        if (!result.success) {
            showToast.error(result.message || 'Registration failed');
            setLoading(false);
            return;
        }

        if (isCorporate) {
            // Shared identity from register, then enter Corporate bootstrap (or invite returnUrl).
            const switched = await switchChannel({ channel: 'Corporate', bootstrap: true });
            if (!switched.success) {
                showToast.error(
                    switched.message ||
                        'Account created, but could not open Corporate. Sign in with Corporate selected.',
                );
                navigate(authPath('/login', { channel: 'corporate', returnUrl }));
                setLoading(false);
                return;
            }
            showToast.success('Account created — set up your company');
            navigate(
                postAuthDestination('corporate', {
                    returnUrl,
                    isBootstrap: true,
                }),
            );
            setLoading(false);
            return;
        }

        // Marketplace register → marketplace dashboard
        navigate(postAuthDestination('marketplace', { returnUrl }));
        setLoading(false);
    };

    const loginLink = authPath('/login', { channel, returnUrl });

    const subtitle = (() => {
        if (returnUrl?.includes('/invite/accept/')) {
            return 'Create an account to accept your company invitation';
        }
        if (isCorporate) {
            return 'Create an account for company parking management';
        }
        return 'Join our parking community';
    })();

    return (
        <div className="auth-page">
            <div className="card auth-card">
                <h1 className="auth-title">Create Account</h1>
                <p className="auth-subtitle">{subtitle}</p>

                <AuthChannelSelector value={channel} onChange={setChannel} />

                <form onSubmit={handleSubmit}>
                    <div className="grid grid-2">
                        <div className="form-group">
                            <label className="form-label">First Name</label>
                            <input
                                type="text"
                                name="firstName"
                                className="form-input"
                                value={formData.firstName}
                                onChange={handleChange}
                                placeholder="John"
                                required
                            />
                        </div>

                        <div className="form-group">
                            <label className="form-label">Last Name</label>
                            <input
                                type="text"
                                name="lastName"
                                className="form-input"
                                value={formData.lastName}
                                onChange={handleChange}
                                placeholder="Doe"
                                required
                            />
                        </div>
                    </div>

                    <div className="form-group">
                        <label className="form-label">Email</label>
                        <input
                            type="email"
                            name="email"
                            className="form-input"
                            value={formData.email}
                            onChange={handleChange}
                            placeholder={isCorporate ? 'work@company.com' : 'john@example.com'}
                            required
                        />
                    </div>

                    <div className="form-group">
                        <label className="form-label">Phone Number</label>
                        <input
                            type="tel"
                            name="phoneNumber"
                            className="form-input"
                            value={formData.phoneNumber}
                            onChange={handleChange}
                            placeholder="+919876543210"
                            required
                        />
                    </div>

                    <div className="form-group">
                        <label className="form-label">Password</label>
                        <input
                            type="password"
                            name="password"
                            className="form-input"
                            value={formData.password}
                            onChange={handleChange}
                            placeholder="Min 8 characters"
                            required
                        />
                    </div>

                    <div className="form-group">
                        <label className="form-label">Confirm Password</label>
                        <input
                            type="password"
                            name="confirmPassword"
                            className="form-input"
                            value={formData.confirmPassword}
                            onChange={handleChange}
                            placeholder="Re-enter password"
                            required
                        />
                    </div>

                    <button type="submit" className="btn btn-primary btn-full" disabled={loading}>
                        {loading
                            ? 'Creating Account...'
                            : isCorporate
                              ? 'Create Corporate Account'
                              : 'Create Account'}
                    </button>
                </form>

                <p className="auth-footer">
                    Already have an account? <Link to={loginLink}>Sign in</Link>
                </p>
            </div>
        </div>
    );
}
