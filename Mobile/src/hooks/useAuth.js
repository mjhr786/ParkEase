/**
 * useAuth Hook
 * Wraps Redux auth state and dispatch for convenient access
 */

import { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { loginThunk, registerThunk, logoutThunk, updateProfileThunk, clearError } from '../store/slices/authSlice';
import { UserRole } from '../utils/constants';

export const useAuth = () => {
    const dispatch = useDispatch();
    const { user, loading, error, isAuthenticated, isSessionChecked } = useSelector(
        (state) => state.auth
    );

    const login = useCallback(
        (credentials) => dispatch(loginThunk(credentials)),
        [dispatch]
    );

    const register = useCallback(
        (data) => dispatch(registerThunk(data)),
        [dispatch]
    );

    const logout = useCallback(
        () => dispatch(logoutThunk()),
        [dispatch]
    );

    const updateProfile = useCallback(
        (data) => dispatch(updateProfileThunk(data)),
        [dispatch]
    );

    const dismissError = useCallback(
        () => dispatch(clearError()),
        [dispatch]
    );

    const isVendor = user?.role === UserRole.Vendor || user?.role === UserRole.Admin;
    const isMember = user?.role === UserRole.Member;

    return {
        user,
        loading,
        error,
        isAuthenticated,
        isSessionChecked,
        isVendor,
        isMember,
        login,
        register,
        logout,
        updateProfile,
        dismissError,
    };
};

export default useAuth;
