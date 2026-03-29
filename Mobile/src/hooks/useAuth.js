/**
 * useAuth Hook
 * Wraps Redux auth state and dispatch for convenient access
 */

import { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { loginThunk, registerThunk, googleLoginThunk, logoutThunk, updateProfileThunk, clearError } from '../store/slices/authSlice';

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

    const googleLogin = useCallback(
        (googleData) => dispatch(googleLoginThunk(googleData)),
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

    /**
     * Ownership helper — determines if the current user is the owner
     * of the parking space in a booking (i.e. the listing owner).
     * If true, the user can approve / reject / manage extensions.
     * If false, the user is the booker and can cancel / extend / pay.
     */
    const isOwnerOfBooking = useCallback(
        (booking) => {
            if (!user || !booking) return false;
            // Check via ownerId first, fallback to comparing userId
            if (booking.ownerId) return booking.ownerId === user.id;
            // If userId matches current user, they are the booker (not owner)
            if (booking.userId) return booking.userId !== user.id;
            return false;
        },
        [user]
    );

    return {
        user,
        loading,
        error,
        isAuthenticated,
        isSessionChecked,
        isOwnerOfBooking,
        login,
        register,
        googleLogin,
        logout,
        updateProfile,
        dismissError,
    };
};

export default useAuth;
