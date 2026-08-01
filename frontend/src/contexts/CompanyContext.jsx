import { createContext, useContext, useState, useEffect } from 'react';
import corporateService from '../services/corporateService';
import { useAuth } from './AuthContext';

const CompanyContext = createContext(null);

/**
 * Company workspace state bound to JWT Corporate channel (PR10b / KD-7).
 *
 * - isCorporateMode === channel === 'Corporate' (never bare localStorage)
 * - activeCompanyId mirrors JWT company_id for corporateService path helpers
 * Soft Personal Mode / activeCompanyId-driven chrome was removed in PR10b.
 * UX rollback after this change = redeploy a prior frontend artifact.
 */
export function CompanyProvider({ children }) {
    const { isAuthenticated, channel, companyId: jwtCompanyId } = useAuth();
    const [activeCompanyId, setActiveCompanyId] = useState(null);
    const [companyDetails, setCompanyDetails] = useState(null);
    const [loadingCompany, setLoadingCompany] = useState(false);

    // Keep company cache aligned with JWT bind — never drive chrome from storage alone
    useEffect(() => {
        if (channel === 'Corporate' && jwtCompanyId) {
            const id = String(jwtCompanyId);
            if (activeCompanyId !== id) {
                localStorage.setItem('activeCompanyId', id);
                setActiveCompanyId(id);
            }
            return;
        }

        if (activeCompanyId) {
            localStorage.removeItem('activeCompanyId');
            setActiveCompanyId(null);
            setCompanyDetails(null);
        }
    }, [channel, jwtCompanyId, activeCompanyId]);

    useEffect(() => {
        if (activeCompanyId && isAuthenticated && channel === 'Corporate') {
            fetchCompanyDetails();
        } else {
            setCompanyDetails(null);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps -- fetch on id/auth/channel only
    }, [activeCompanyId, isAuthenticated, channel]);

    const fetchCompanyDetails = async () => {
        setLoadingCompany(true);
        try {
            const res = await corporateService.getCompany();
            if (res.success) {
                setCompanyDetails(res.data);
            } else {
                clearActiveCompany();
            }
        } catch (error) {
            console.error('Failed to fetch company details', error);
        } finally {
            setLoadingCompany(false);
        }
    };

    /** Cache helper after channel re-mint — does not change product channel by itself. */
    const switchCompany = (companyId) => {
        if (companyId) {
            localStorage.setItem('activeCompanyId', companyId);
            setActiveCompanyId(companyId);
        } else {
            clearActiveCompany();
        }
    };

    const clearActiveCompany = () => {
        localStorage.removeItem('activeCompanyId');
        setActiveCompanyId(null);
        setCompanyDetails(null);
    };

    const isCorporateMode = channel === 'Corporate';

    return (
        <CompanyContext.Provider
            value={{
                activeCompanyId,
                companyDetails,
                isCorporateMode,
                loadingCompany,
                switchCompany,
                clearActiveCompany,
                refreshCompanyDetails: fetchCompanyDetails,
            }}
        >
            {children}
        </CompanyContext.Provider>
    );
}

export function useCompany() {
    const context = useContext(CompanyContext);
    if (!context) {
        throw new Error('useCompany must be used within a CompanyProvider');
    }
    return context;
}
