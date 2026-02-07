import { createContext, useContext, useState, useEffect } from 'react';
import api from '../services/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const storedUser = localStorage.getItem('user');
        if (storedUser) {
            setUser(JSON.parse(storedUser));
        }
        setLoading(false);
    }, []);

    const login = async (email, password) => {
        try {
            const response = await api.login({ email, password });
            if (response.success && response.data) {
                const { accessToken, refreshToken, user: userData } = response.data;
                api.setTokens(accessToken, refreshToken);
                localStorage.setItem('user', JSON.stringify(userData));
                setUser(userData);
                return { success: true };
            }
            return { success: false, message: response.message };
        } catch (error) {
            return { success: false, message: error.message };
        }
    };

    const register = async (data) => {
        try {
            const response = await api.register(data);
            if (response.success && response.data) {
                const { accessToken, refreshToken, user: userData } = response.data;
                api.setTokens(accessToken, refreshToken);
                localStorage.setItem('user', JSON.stringify(userData));
                setUser(userData);
                return { success: true };
            }
            return { success: false, message: response.message };
        } catch (error) {
            return { success: false, message: error.message };
        }
    };

    const logout = async () => {
        try {
            await api.logout();
        } catch (error) {
            console.log('Logout error:', error);
        }
        api.clearTokens();
        setUser(null);
    };

    const isVendor = user?.role === 1 || user?.role === 'Vendor';
    const isAdmin = user?.role === 0 || user?.role === 'Admin';
    const isMember = user?.role === 2 || user?.role === 'Member';

    return (
        <AuthContext.Provider value={{
            user,
            login,
            register,
            logout,
            loading,
            isVendor,
            isAdmin,
            isMember,
            isAuthenticated: !!user
        }}>
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
