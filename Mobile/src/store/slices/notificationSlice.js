
import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';

export const getNotificationsThunk = createAsyncThunk(
    'notification/getNotifications',
    async () => { return []; }
);

const notificationSlice = createSlice({
    name: 'notification',
    initialState: { unreadCount: 0 },
    reducers: {}
});

export default notificationSlice.reducer;
