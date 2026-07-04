import { createContext, useContext, useState, useEffect } from 'react';
import corporateService from '../services/corporateService';
import { useAuth } from './AuthContext';

const CompanyContext = createContext(null);

export function CompanyProvider({ children }) {
    const { isAuthenticated } = useAuth();
    const [activeCompanyId, setActiveCompanyId] = useState(() => {
        return localStorage.getItem('activeCompanyId') || null;
    });
    const [companyDetails, setCompanyDetails] = useState(null);
    const [loadingCompany, setLoadingCompany] = useState(false);

    useEffect(() => {
        if (activeCompanyId && isAuthenticated) {
            fetchCompanyDetails();
        } else {
            setCompanyDetails(null);
        }
    }, [activeCompanyId, isAuthenticated]);

    const fetchCompanyDetails = async () => {
        setLoadingCompany(true);
        try {
            const res = await corporateService.getCompany();
            if (res.success) {
                setCompanyDetails(res.data);
            } else {
                // Invalid or removed from company
                clearActiveCompany();
            }
        } catch (error) {
            console.error("Failed to fetch company details", error);
        } finally {
            setLoadingCompany(false);
        }
    };

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

    return (
        <CompanyContext.Provider value={{
            activeCompanyId,
            companyDetails,
            isCorporateMode: !!activeCompanyId,
            loadingCompany,
            switchCompany,
            clearActiveCompany,
            refreshCompanyDetails: fetchCompanyDetails
        }}>
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
