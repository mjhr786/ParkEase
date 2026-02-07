import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function Register() {
    const [formData, setFormData] = useState({
        email: '',
        password: '',
        confirmPassword: '',
        firstName: '',
        lastName: '',
        phoneNumber: '',
        role: 2, // Member by default
    });
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const { register } = useAuth();
    const navigate = useNavigate();

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');

        if (formData.password !== formData.confirmPassword) {
            setError('Passwords do not match');
            return;
        }

        if (formData.password.length < 8) {
            setError('Password must be at least 8 characters');
            return;
        }

        setLoading(true);

        const { confirmPassword, ...registerData } = formData;
        registerData.role = parseInt(registerData.role);

        const result = await register(registerData);

        if (result.success) {
            navigate('/dashboard');
        } else {
            setError(result.message || 'Registration failed');
        }

        setLoading(false);
    };

    return (
        <div className="auth-page">
            <div className="card auth-card">
                <h1 className="auth-title">Create Account</h1>
                <p className="auth-subtitle">Join our parking community</p>

                {error && <div className="alert alert-error">{error}</div>}

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
                            placeholder="john@example.com"
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
                        <label className="form-label">I want to</label>
                        <select
                            name="role"
                            className="form-select"
                            value={formData.role}
                            onChange={handleChange}
                        >
                            <option value={2}>Find Parking (Member)</option>
                            <option value={1}>Rent Out My Parking (Vendor)</option>
                        </select>
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
                        {loading ? 'Creating Account...' : 'Create Account'}
                    </button>
                </form>

                <p className="auth-footer">
                    Already have an account? <Link to="/login">Sign in</Link>
                </p>
            </div>
        </div>
    );
}
