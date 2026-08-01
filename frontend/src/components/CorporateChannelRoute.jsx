import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

function Loading() {
  return (
    <div
      className="loading"
      style={{ minHeight: '60vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}
    >
      <div className="spinner" />
    </div>
  );
}

/**
 * Guard for /corporate/* app routes (PR7 / PR10b).
 *
 * JWT channel must be Corporate. Soft-mode (auth-only) access was removed in PR10b —
 * UX rollback = prior frontend artifact.
 *
 * @param {{ children: React.ReactNode, allowBootstrap?: boolean }} props
 *   allowBootstrap — create-company may run with Corporate + isBootstrap and no companyId yet.
 */
export default function CorporateChannelRoute({ children, allowBootstrap = false }) {
  const { isAuthenticated, loading, channel, companyId, isBootstrap } = useAuth();
  const location = useLocation();

  if (loading) {
    return <Loading />;
  }

  const returnUrl = `${location.pathname}${location.search || ''}`;
  const corporateLoginTo =
    returnUrl && returnUrl !== '/'
      ? `/corporate/login?returnUrl=${encodeURIComponent(returnUrl)}`
      : '/corporate/login';

  if (!isAuthenticated) {
    return <Navigate to={corporateLoginTo} replace />;
  }

  if (channel !== 'Corporate') {
    return <Navigate to={corporateLoginTo} replace />;
  }

  if (!companyId) {
    if (allowBootstrap || isBootstrap) {
      if (allowBootstrap) {
        return children;
      }
      return <Navigate to="/corporate/create-company" replace />;
    }
    return <Navigate to={corporateLoginTo} replace />;
  }

  return children;
}
