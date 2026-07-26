
import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';

export const getUnreadCountThunk = createAsyncThunk(
    'chat/getUnreadCount',
    async () => { return 0; }
);

const chatSlice = createSlice({
    name: 'chat',
    initialState: { unreadCount: 0 },
    reducers: {}
});

export default chatSlice.reducer;
