/**
 * Auth Slice
 * Authentication state management with thunks
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import authService from '../../services/auth/authService';
import { getErrorMessage } from '../../utils/errorHandler';

/**
 * Login thunk
 */
export const loginThunk = createAsyncThunk(
    'auth/login',
    async (credentials, { rejectWithValue }) => {
        try {
            const result = await authService.login(credentials);
            if (!result.success) {
                return rejectWithValue(result.message || 'Login failed');
            }
            return result.data;
        } catch (error) {
            console.log('--- API DEBUG LOG ---');
            console.log('Login Error Raw:', JSON.stringify(error, Object.getOwnPropertyNames(error), 2));
            console.log('Login Error Data:', JSON.stringify(error.response?.data, null, 2));
            console.log('---------------------');
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

/**
 * Register thunk
 */
export const registerThunk = createAsyncThunk(
    'auth/register',
    async (data, { rejectWithValue }) => {
        try {
            const result = await authService.register(data);
            if (!result.success) {
                return rejectWithValue(result.message || 'Registration failed');
            }
            return result.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

/**
 * Google Login thunk
 */
export const googleLoginThunk = createAsyncThunk(
    'auth/googleLogin',
    async (googleData, { rejectWithValue }) => {
        try {
            const result = await authService.googleLogin(googleData);
            if (!result.success) {
                return rejectWithValue(result.message || 'Google login failed');
            }
            return result.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

/**
 * Logout thunk
 */
export const logoutThunk = createAsyncThunk(
    'auth/logout',
    async (_, { rejectWithValue }) => {
        try {
            await authService.logout();
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

/**
 * Restore session thunk
 */
export const restoreSessionThunk = createAsyncThunk(
    'auth/restoreSession',
    async (_, { rejectWithValue }) => {
        try {
            const user = await authService.tryRestoreSession();
            if (!user) {
                return rejectWithValue('No active session');
            }
            return user;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

/**
 * Update profile thunk
 */
export const updateProfileThunk = createAsyncThunk(
    'auth/updateProfile',
    async (data, { rejectWithValue }) => {
        try {
            const result = await authService.updateProfile(data);
            if (!result.success) {
                return rejectWithValue(result.message || 'Update failed');
            }
            return result.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

/**
 * Change password thunk
 */
export const changePasswordThunk = createAsyncThunk(
    'auth/changePassword',
    async (data, { rejectWithValue }) => {
        try {
            const result = await authService.changePassword(data);
            if (!result.success) {
                return rejectWithValue(result.message || 'Password change failed');
            }
            return result.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

/**
 * Delete account thunk
 */
export const deleteAccountThunk = createAsyncThunk(
    'auth/deleteAccount',
    async (_, { rejectWithValue }) => {
        try {
            const result = await authService.deleteAccount();
            if (!result.success) {
                return rejectWithValue(result.message || 'Delete failed');
            }
            return result.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    user: null,
    token: null,
    loading: false,
    error: null,
    isAuthenticated: false,
    isSessionChecked: false,
};

const authSlice = createSlice({
    name: 'auth',
    initialState,
    reducers: {
        clearError: (state) => {
            state.error = null;
        },
        resetAuth: () => initialState,
        sessionExpired: (state) => {
            Object.assign(state, { ...initialState, isSessionChecked: true });
        },
    },
    extraReducers: (builder) => {
        builder
            // Login
            .addCase(loginThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(loginThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.user = action.payload.user;
                state.token = action.payload.accessToken;
                state.isAuthenticated = true;
                state.isSessionChecked = true;
            })
            .addCase(loginThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
                state.isAuthenticated = false;
            })
            // Register
            .addCase(registerThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(registerThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.user = action.payload.user;
                state.token = action.payload.accessToken;
                state.isAuthenticated = true;
                state.isSessionChecked = true;
            })
            .addCase(registerThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            // Google Login
            .addCase(googleLoginThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(googleLoginThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.user = action.payload.user;
                state.token = action.payload.accessToken;
                state.isAuthenticated = true;
                state.isSessionChecked = true;
            })
            .addCase(googleLoginThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            // Logout
            .addCase(logoutThunk.fulfilled, (state) => {
                Object.assign(state, { ...initialState, isSessionChecked: true });
            })
            // Restore Session
            .addCase(restoreSessionThunk.pending, (state) => {
                state.loading = true;
            })
            .addCase(restoreSessionThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.user = action.payload;
                state.isAuthenticated = true;
                state.isSessionChecked = true;
            })
            .addCase(restoreSessionThunk.rejected, (state) => {
                state.loading = false;
                state.isAuthenticated = false;
                state.isSessionChecked = true;
            })
            // Update Profile
            .addCase(updateProfileThunk.fulfilled, (state, action) => {
                state.user = action.payload;
            })
            // Change Password
            .addCase(changePasswordThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(changePasswordThunk.fulfilled, (state) => {
                state.loading = false;
            })
            .addCase(changePasswordThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            // Delete Account
            .addCase(deleteAccountThunk.fulfilled, (state) => {
                Object.assign(state, { ...initialState, isSessionChecked: true });
            });
    },
});

export const { clearError, resetAuth, sessionExpired } = authSlice.actions;
export default authSlice.reducer;
