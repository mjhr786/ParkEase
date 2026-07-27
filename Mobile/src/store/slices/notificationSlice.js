import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import notificationApiService from '../../services/api/notificationApiService';

/**
 * Thunks
 */
export const getNotificationsThunk = createAsyncThunk(
    'notification/getNotifications',
    async (_, { rejectWithValue }) => {
        try {
            const response = await notificationApiService.getNotifications();
            if (response.success) {
                return response.data; // Expected: array of notifications
            }
            return rejectWithValue(response.message || 'Failed to fetch notifications');
        } catch (error) {
            return rejectWithValue(error.message);
        }
    }
);

export const markAsReadThunk = createAsyncThunk(
    'notification/markAsRead',
    async (id, { rejectWithValue }) => {
        try {
            const response = await notificationApiService.markAsRead(id);
            if (response.success) {
                return id;
            }
            return rejectWithValue(response.message || 'Failed to mark as read');
        } catch (error) {
            return rejectWithValue(error.message);
        }
    }
);

export const markAllAsReadThunk = createAsyncThunk(
    'notification/markAllAsRead',
    async (_, { rejectWithValue }) => {
        try {
            const response = await notificationApiService.markAllAsRead();
            if (response.success) {
                return true;
            }
            return rejectWithValue(response.message || 'Failed to mark all as read');
        } catch (error) {
            return rejectWithValue(error.message);
        }
    }
);

export const deleteNotificationThunk = createAsyncThunk(
    'notification/deleteNotification',
    async (id, { rejectWithValue }) => {
        try {
            const response = await notificationApiService.deleteNotification(id);
            if (response.success) {
                return id;
            }
            return rejectWithValue(response.message || 'Failed to delete notification');
        } catch (error) {
            return rejectWithValue(error.message);
        }
    }
);

export const clearAllNotificationsThunk = createAsyncThunk(
    'notification/clearAllNotifications',
    async (_, { rejectWithValue }) => {
        try {
            const response = await notificationApiService.clearAll();
            if (response.success) {
                return true;
            }
            return rejectWithValue(response.message || 'Failed to clear notifications');
        } catch (error) {
            return rejectWithValue(error.message);
        }
    }
);

const initialState = {
    items: [],
    unreadCount: 0,
    loading: false,
    error: null,
};

const notificationSlice = createSlice({
    name: 'notification',
    initialState,
    reducers: {
        // Reducer to manually push a notification when received via WebSocket or Push
        addNotification: (state, action) => {
            state.items.unshift(action.payload);
            if (!action.payload.isRead) {
                state.unreadCount += 1;
            }
        },
    },
    extraReducers: (builder) => {
        builder
            // getNotifications
            .addCase(getNotificationsThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(getNotificationsThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.items = action.payload || [];
                state.unreadCount = state.items.filter(n => !n.isRead).length;
            })
            .addCase(getNotificationsThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            
            // markAsRead
            .addCase(markAsReadThunk.fulfilled, (state, action) => {
                const id = action.payload;
                const index = state.items.findIndex(n => n.id === id);
                if (index !== -1 && !state.items[index].isRead) {
                    state.items[index].isRead = true;
                    state.unreadCount = Math.max(0, state.unreadCount - 1);
                }
            })

            // markAllAsRead
            .addCase(markAllAsReadThunk.fulfilled, (state) => {
                state.items.forEach(n => { n.isRead = true; });
                state.unreadCount = 0;
            })

            // deleteNotification
            .addCase(deleteNotificationThunk.fulfilled, (state, action) => {
                const id = action.payload;
                const notification = state.items.find(n => n.id === id);
                if (notification && !notification.isRead) {
                    state.unreadCount = Math.max(0, state.unreadCount - 1);
                }
                state.items = state.items.filter(n => n.id !== id);
            })

            // clearAllNotifications
            .addCase(clearAllNotificationsThunk.fulfilled, (state) => {
                state.items = [];
                state.unreadCount = 0;
            });
    }
});

export const { addNotification } = notificationSlice.actions;
export default notificationSlice.reducer;
