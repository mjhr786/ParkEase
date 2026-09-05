import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import api from '../../services/api';
import showToast from '../../utils/toast.jsx';

/**
 * Corporate SSO post-IdP complete page.
 * Receives ?sso_code= from API callback redirect, exchanges for session, applySession.
 * Never treats marketplace social tokens as corporate.
 */
export default function CorporateSsoComplete() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { applySession } = useAuth();
  const [status, setStatus] = useState('exchanging');
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    const code = searchParams.get('sso_code') || searchParams.get('exchangeCode');

    async function complete() {
      if (!code) {
        setStatus('error');
        setError('Missing SSO completion code. Start again from Corporate login.');
        return;
      }

      try {
        const response = await api.corporateSsoComplete({ exchangeCode: code, ssoCode: code });
        if (cancelled) return;

        if (response.success && response.data?.session) {
          applySession(response.data.session);
          showToast.success('Signed in with company SSO');
          setStatus('success');
          navigate('/corporate/dashboard', { replace: true });
          return;
        }

        setStatus('error');
        setError(
          response.message ||
            response.code ||
            'Could not complete company SSO. Try again from Corporate login.'
        );
      } catch (err) {
        if (cancelled) return;
        setStatus('error');
        setError(err?.message || 'SSO completion failed');
      }
    }

    complete();
    return () => {
      cancelled = true;
    };
  }, [searchParams, applySession, navigate]);

  return (
    <div className="auth-page">
      <div className="card auth-card">
        <h1 className="auth-title">Company SSO</h1>
        {status === 'exchanging' && (
          <p className="auth-subtitle">Completing secure sign-in…</p>
        )}
        {status === 'error' && (
          <>
            <p className="auth-subtitle" style={{ color: 'var(--color-danger, #b91c1c)' }}>
              {error}
            </p>
            <Link className="btn btn-primary btn-full" to="/corporate/login">
              Back to Corporate login
            </Link>
          </>
        )}
      </div>
    </div>
  );
}
