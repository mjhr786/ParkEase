import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import showToast from '../utils/toast.jsx';
import { safeReturnUrl } from '../utils/safeReturnUrl';

export default function Login() {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [loading, setLoading] = useState(false);
    const { login, isAdmin } = useAuth();
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const returnUrl = safeReturnUrl(searchParams.get('returnUrl'));

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);

        const result = await login(email, password);

        if (result.success) {
            // Prefer explicit returnUrl; otherwise platform admins land in admin shell (not consumer dashboard).
            if (returnUrl) {
                navigate(returnUrl);
            } else {
                const stored = localStorage.getItem('user');
                let admin = false;
                try {
                    const u = stored ? JSON.parse(stored) : null;
                    admin = u?.role === 0 || u?.role === 'Admin';
                } catch { /* ignore */ }
                navigate(admin || isAdmin ? '/admin' : '/dashboard');
            }
        } else {
            showToast.error(result.message || 'Login failed');
        }

        setLoading(false);
    };

    const registerLink = returnUrl
        ? `/register?returnUrl=${encodeURIComponent(returnUrl)}`
        : '/register';

    return (
        <div className="auth-page">
            <div className="card auth-card">
                <h1 className="auth-title">Welcome Back</h1>
                <p className="auth-subtitle">
                    {returnUrl?.includes('/invite/accept/')
                        ? 'Sign in to accept your company invitation'
                        : 'Sign in to your account'}
                </p>

                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label className="form-label">Email</label>
                        <input
                            type="email"
                            className="form-input"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            placeholder="Enter your email"
                            required
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
                        />
                    </div>

                    <button type="submit" className="btn btn-primary btn-full" disabled={loading}>
                        {loading ? 'Signing in...' : 'Sign In'}
                    </button>
                </form>

                <p className="auth-footer">
                    Don&apos;t have an account? <Link to={registerLink}>Sign up</Link>
                </p>
            </div>
        </div>
    );
}
