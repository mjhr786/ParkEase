import { Navigate, useSearchParams } from 'react-router-dom';

/**
 * Legacy corporate entry path. Auth channel selection now lives on /login.
 * Preserves returnUrl / companyId while forcing channel=corporate.
 */
export default function CorporateLogin() {
    const [searchParams] = useSearchParams();
    const params = new URLSearchParams(searchParams);
    params.set('channel', 'corporate');
    const qs = params.toString();
    return <Navigate to={qs ? `/login?${qs}` : '/login?channel=corporate'} replace />;
}
