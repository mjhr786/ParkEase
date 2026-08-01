import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import api from '../services/api';
import { getErrorMessage } from '../utils/errorHandler';

const AuthContext = createContext(null);

function readStoredChannelState() {
    return {
        channel: localStorage.getItem('channel') || null,
        companyId: localStorage.getItem('companyId') || null,
        companyRole: localStorage.getItem('companyRole') || null,
        isBootstrap: localStorage.getItem('isBootstrap') === 'true',
        isolationEnabled: localStorage.getItem('isolationEnabled') === 'true',
    };
}

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);
    const [channel, setChannel] = useState(null);
    const [companyId, setCompanyId] = useState(null);
    const [companyRole, setCompanyRole] = useState(null);
    const [isBootstrap, setIsBootstrap] = useState(false);
    const [isolationEnabled, setIsolationEnabled] = useState(false);

    const syncChannelStateFromStorage = useCallback(() => {
        const s = readStoredChannelState();
        setChannel(s.channel);
        setCompanyId(s.companyId);
        setCompanyRole(s.companyRole);
        setIsBootstrap(s.isBootstrap);
        setIsolationEnabled(s.isolationEnabled);
    }, []);

    /**
     * Apply TokenDto (or equivalent) to storage + React state.
     * @param {object} session
     */
    const applySession = useCallback((session) => {
        const applied = api.applySession(session);
        if (applied.user) {
            setUser(applied.user);
        } else {
            const stored = localStorage.getItem('user');
            if (stored) {
                try {
                    setUser(JSON.parse(stored));
                } catch {
                    /* ignore */
                }
            }
        }
        setChannel(applied.channel);
        setCompanyId(applied.companyId);
        setCompanyRole(applied.companyRole);
        setIsBootstrap(applied.isBootstrap);
        return { success: true, ...applied };
    }, []);

    const clearAuthState = useCallback(() => {
        setUser(null);
        setChannel(null);
        setCompanyId(null);
        setCompanyRole(null);
        setIsBootstrap(false);
        setIsolationEnabled(false);
    }, []);

    useEffect(() => {
        const storedUser = localStorage.getItem('user');
        if (storedUser) {
            try {
                setUser(JSON.parse(storedUser));
            } catch {
                localStorage.removeItem('user');
            }
        }
        syncChannelStateFromStorage();
        setLoading(false);
    }, [syncChannelStateFromStorage]);

    // Load isolationEnabled from channel-context when already authenticated
    useEffect(() => {
        if (!user || !api.getToken()) return undefined;
        let cancelled = false;
        (async () => {
            try {
                const res = await api.getChannelContext();
                if (cancelled || !res?.success || !res.data) return;
                const enabled = !!res.data.isolationEnabled;
                localStorage.setItem('isolationEnabled', enabled ? 'true' : 'false');
                setIsolationEnabled(enabled);
                if (res.data.channel) {
                    localStorage.setItem('channel', res.data.channel);
                    setChannel(res.data.channel);
                }
                if (res.data.companyId) {
                    localStorage.setItem('companyId', String(res.data.companyId));
                    setCompanyId(String(res.data.companyId));
                }
                if (res.data.companyRole) {
                    localStorage.setItem('companyRole', res.data.companyRole);
                    setCompanyRole(res.data.companyRole);
                }
                setIsBootstrap(!!res.data.isBootstrap);
                localStorage.setItem('isBootstrap', res.data.isBootstrap ? 'true' : 'false');
            } catch {
                // Offline / 401 — leave stored isolation flag
            }
        })();
        return () => {
            cancelled = true;
        };
    }, [user]);

    const login = async (email, password) => {
        try {
            const response = await api.login({ email, password });
            if (response.success && response.data) {
                applySession(response.data);
                return { success: true, channel: response.data.channel || 'Marketplace' };
            }
            return { success: false, message: getErrorMessage(response), errors: response.errors, code: response.code };
        } catch (error) {
            return {
                success: false,
                message: error.response?.data ? getErrorMessage(error.response.data) : error.message,
                errors: error.response?.data?.errors,
                code: error.code || error.response?.data?.code,
            };
        }
    };

    /**
     * Corporate product login. May return tokens, bootstrap session, or company selection required.
     * @returns {{ success: boolean, isBootstrap?: boolean, requiresCompanySelection?: boolean, memberships?: array, message?: string, code?: string }}
     */
    const loginCorporate = async (email, password, selectedCompanyId = null) => {
        try {
            const response = await api.loginCorporate({
                email,
                password,
                companyId: selectedCompanyId || undefined,
            });
            if (response.success && response.data) {
                const data = response.data;
                if (data.requiresCompanySelection) {
                    return {
                        success: false,
                        requiresCompanySelection: true,
                        memberships: data.memberships || [],
                        message: response.message || 'Select a company to continue',
                        code: response.code || 'company_selection_required',
                    };
                }
                if (data.session) {
                    applySession(data.session);
                    return {
                        success: true,
                        isBootstrap: !!data.isBootstrap || !!data.session.isBootstrap,
                        channel: data.session.channel || 'Corporate',
                        companyId: data.session.companyId ? String(data.session.companyId) : null,
                    };
                }
                return {
                    success: false,
                    message: response.message || 'Corporate login failed',
                    code: response.code,
                };
            }
            return {
                success: false,
                message: getErrorMessage(response),
                errors: response.errors,
                code: response.code,
                requiresCompanySelection: response.code === 'company_selection_required',
                memberships: response.data?.memberships,
            };
        } catch (error) {
            const data = error.response?.data;
            return {
                success: false,
                message: data ? getErrorMessage(data) : error.message,
                errors: data?.errors,
                code: error.code || data?.code,
                requiresCompanySelection: (error.code || data?.code) === 'company_selection_required',
                memberships: data?.data?.memberships || data?.memberships,
            };
        }
    };

    /**
     * Switch product channel / re-bind company (including bootstrap → bound).
     */
    const switchChannel = async ({ channel: targetChannel, companyId: targetCompanyId, bootstrap } = {}) => {
        try {
            const response = await api.switchChannel({
                channel: targetChannel,
                companyId: targetCompanyId,
                bootstrap,
            });
            if (response.success && response.data) {
                applySession(response.data);
                return {
                    success: true,
                    channel: response.data.channel,
                    companyId: response.data.companyId ? String(response.data.companyId) : null,
                    isBootstrap: !!response.data.isBootstrap,
                };
            }
            return {
                success: false,
                message: getErrorMessage(response),
                errors: response.errors,
                code: response.code,
            };
        } catch (error) {
            return {
                success: false,
                message: error.response?.data ? getErrorMessage(error.response.data) : error.message,
                errors: error.response?.data?.errors,
                code: error.code || error.response?.data?.code,
            };
        }
    };

    const refreshChannelContext = async () => {
        try {
            const res = await api.getChannelContext();
            if (res.success && res.data) {
                const enabled = !!res.data.isolationEnabled;
                localStorage.setItem('isolationEnabled', enabled ? 'true' : 'false');
                setIsolationEnabled(enabled);
                if (res.data.channel) {
                    localStorage.setItem('channel', res.data.channel);
                    setChannel(res.data.channel);
                }
                setCompanyId(res.data.companyId ? String(res.data.companyId) : null);
                if (res.data.companyId) {
                    localStorage.setItem('companyId', String(res.data.companyId));
                } else {
                    localStorage.removeItem('companyId');
                }
                setCompanyRole(res.data.companyRole || null);
                if (res.data.companyRole) {
                    localStorage.setItem('companyRole', res.data.companyRole);
                } else {
                    localStorage.removeItem('companyRole');
                }
                setIsBootstrap(!!res.data.isBootstrap);
                localStorage.setItem('isBootstrap', res.data.isBootstrap ? 'true' : 'false');
                return { success: true, data: res.data };
            }
            return { success: false, message: getErrorMessage(res) };
        } catch (error) {
            return {
                success: false,
                message: error.response?.data ? getErrorMessage(error.response.data) : error.message,
            };
        }
    };

    const register = async (data) => {
        try {
            const response = await api.register(data);
            if (response.success && response.data) {
                applySession(response.data);
                return { success: true };
            }
            return { success: false, message: getErrorMessage(response), errors: response.errors };
        } catch (error) {
            return {
                success: false,
                message: error.response?.data ? getErrorMessage(error.response.data) : error.message,
                errors: error.response?.data?.errors,
            };
        }
    };

    const logout = async () => {
        try {
            await api.logout();
        } catch {
            // ignore network errors on logout
        }
        api.clearTokens();
        clearAuthState();
    };

    const updateUser = (userData) => {
        const updated = { ...user, ...userData };
        localStorage.setItem('user', JSON.stringify(updated));
        setUser(updated);
    };

    const isAdmin = user?.role === 0 || user?.role === 'Admin';
    const isCorporateChannel = channel === 'Corporate';

    return (
        <AuthContext.Provider
            value={{
                user,
                login,
                loginCorporate,
                switchChannel,
                applySession,
                refreshChannelContext,
                register,
                logout,
                updateUser,
                loading,
                isAdmin,
                isAuthenticated: !!user,
                channel,
                companyId,
                companyRole,
                isBootstrap,
                isolationEnabled,
                isCorporateChannel,
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
}

/** True when API error is channel isolation denial. */
export function isChannelForbiddenError(errorOrResult) {
    if (!errorOrResult) return false;
    if (errorOrResult.channelForbidden) return true;
    if (errorOrResult.code === 'channel_forbidden') return true;
    return api.constructor.isChannelForbidden?.(errorOrResult.response?.data)
        || api.constructor.isChannelForbidden?.(errorOrResult);
}
