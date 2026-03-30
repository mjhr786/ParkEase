/**
 * Notification Slice
 * State for user notifications
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getNotificationsThunk = createAsyncThunk(
    'notification/getAll',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.NOTIFICATIONS.BASE);
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const markNotificationReadThunk = createAsyncThunk(
    'notification/markRead',
    async (id, { rejectWithValue }) => {
        try {
            await apiClient.put(ENDPOINTS.NOTIFICATIONS.MARK_READ(id));
            return id;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const deleteNotificationThunk = createAsyncThunk(
    'notification/delete',
    async (id, { rejectWithValue }) => {
        try {
            await apiClient.delete(ENDPOINTS.NOTIFICATIONS.DELETE(id));
            return id;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const markAllNotificationsAsReadThunk = createAsyncThunk(
    'notification/markAllRead',
    async (_, { rejectWithValue }) => {
        try {
            await apiClient.put(ENDPOINTS.NOTIFICATIONS.MARK_ALL_READ);
            return true;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const clearAllNotificationsThunk = createAsyncThunk(
    'notification/clearAll',
    async (_, { rejectWithValue }) => {
        try {
            await apiClient.delete(ENDPOINTS.NOTIFICATIONS.CLEAR_ALL);
            return true;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    notifications: [],
    unreadCount: 0,
    loading: false,
    error: null,
};

const notificationSlice = createSlice({
    name: 'notification',
    initialState,
    reducers: {
        clearNotifications: () => initialState,
    },
    extraReducers: (builder) => {
        builder
            .addCase(getNotificationsThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(getNotificationsThunk.fulfilled, (state, action) => {
                state.loading = false;
                // Backend returns { notifications: { items: [], ... }, unreadCount: X }
                const data = action.payload;
                if (data?.notifications?.items) {
                    state.notifications = data.notifications.items;
                    state.unreadCount = data.unreadCount || 0;
                } else {
                    state.notifications = Array.isArray(data) ? data : [];
                    state.unreadCount = state.notifications.filter(n => !n.isRead).length;
                }
            })
            .addCase(getNotificationsThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(markNotificationReadThunk.fulfilled, (state, action) => {
                const idx = state.notifications.findIndex((n) => n.id === action.payload);
                if (idx !== -1) {
                    state.notifications[idx].isRead = true;
                    state.unreadCount = Math.max(0, state.unreadCount - 1);
                }
            })
            .addCase(deleteNotificationThunk.fulfilled, (state, action) => {
                state.notifications = state.notifications.filter((n) => n.id !== action.payload);
            })
            .addCase(markAllNotificationsAsReadThunk.fulfilled, (state) => {
                state.notifications.forEach((n) => {
                    n.isRead = true;
                });
                state.unreadCount = 0;
            })
            .addCase(clearAllNotificationsThunk.fulfilled, (state) => {
                state.notifications = [];
                state.unreadCount = 0;
            });
    },
});

export const { clearNotifications } = notificationSlice.actions;
export default notificationSlice.reducer;
