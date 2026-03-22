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

const initialState = {
    notifications: [],
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
                state.notifications = action.payload?.notifications || action.payload || [];
            })
            .addCase(getNotificationsThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(markNotificationReadThunk.fulfilled, (state, action) => {
                const idx = state.notifications.findIndex((n) => n.id === action.payload);
                if (idx !== -1) {
                    state.notifications[idx].isRead = true;
                }
            })
            .addCase(deleteNotificationThunk.fulfilled, (state, action) => {
                state.notifications = state.notifications.filter((n) => n.id !== action.payload);
            });
    },
});

export const { clearNotifications } = notificationSlice.actions;
export default notificationSlice.reducer;
