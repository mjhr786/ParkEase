import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import corporateService from '../../services/corporateService';
import { useAuth } from '../../contexts/AuthContext';
import { useCompany } from '../../contexts/CompanyContext';
import toast from 'react-hot-toast';

const AcceptInvitation = () => {
    const { token } = useParams();
    const navigate = useNavigate();
    const { isAuthenticated } = useAuth();
    const { loadCompanies } = useCompany();
    
    const [status, setStatus] = useState('processing'); // processing, success, error
    const [errorMessage, setErrorMessage] = useState('');

    useEffect(() => {
        if (!isAuthenticated) {
            toast.error('Please login to accept the invitation.');
            navigate('/login');
            return;
        }

        const acceptInvite = async () => {
            try {
                const response = await corporateService.acceptInvitation(token);
                if (response.success) {
                    setStatus('success');
                    toast.success('Waitlist/Invitation accepted successfully! You are now part of the company.');
                    await loadCompanies(); // Refresh the company switcher list
                    setTimeout(() => navigate('/dashboard'), 3000);
                } else {
                    setStatus('error');
                    setErrorMessage(response.message || 'The invitation link is invalid or has expired.');
                }
            } catch (error) {
                setStatus('error');
                setErrorMessage('An unexpected error occurred while communicating with the server.');
            }
        };

        if (token) {
            acceptInvite();
        } else {
            setStatus('error');
            setErrorMessage('No invitation token provided.');
        }
    }, [token, isAuthenticated]);

    return (
        <div className="container" style={{ minHeight: '60vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <div style={{ background: '#1e293b', border: '1px solid rgba(255,255,255,0.05)', borderRadius: '16px', padding: '3rem', width: '100%', maxWidth: '500px', textAlign: 'center' }}>
                
                {status === 'processing' && (
                    <>
                        <div className="spinner" style={{ margin: '0 auto 1.5rem auto' }}></div>
                        <h2 style={{ color: 'white', marginBottom: '1rem' }}>Processing Invitation...</h2>
                        <p style={{ color: '#94a3b8' }}>Please wait while we link your account to the corporate tenant.</p>
                    </>
                )}

                {status === 'success' && (
                    <>
                        <div style={{ fontSize: '4rem', marginBottom: '1rem' }}>🎉</div>
                        <h2 style={{ color: '#10b981', marginBottom: '1rem' }}>Welcome Aboard!</h2>
                        <p style={{ color: '#cbd5e1', marginBottom: '2rem' }}>
                            Your corporate invitation has been accepted. You can now switch to the Corporate Mode from the header dropdown to access enterprise parking benefits.
                        </p>
                        <p style={{ color: '#64748b', fontSize: '0.85rem' }}>Redirecting to dashboard...</p>
                    </>
                )}

                {status === 'error' && (
                    <>
                        <div style={{ fontSize: '4rem', marginBottom: '1rem' }}>❌</div>
                        <h2 style={{ color: '#ef4444', marginBottom: '1rem' }}>Invitation Failed</h2>
                        <p style={{ color: '#cbd5e1', marginBottom: '2rem' }}>{errorMessage}</p>
                        <button 
                            className="btn btn-primary"
                            onClick={() => navigate('/dashboard')}
                        >
                            Return to Dashboard
                        </button>
                    </>
                )}
            </div>
        </div>
    );
};

export default AcceptInvitation;
