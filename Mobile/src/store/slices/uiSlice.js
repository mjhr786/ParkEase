/**
 * UI Slice
 * Global UI state: loading overlay, toast messages
 */

import { createSlice } from '@reduxjs/toolkit';

const initialState = {
    globalLoading: false,
    toast: null, // { message, type: 'success' | 'error' | 'info' }
};

const uiSlice = createSlice({
    name: 'ui',
    initialState,
    reducers: {
        setGlobalLoading: (state, action) => {
            state.globalLoading = action.payload;
        },
        showToast: (state, action) => {
            state.toast = action.payload;
        },
        hideToast: (state) => {
            state.toast = null;
        },
    },
});

export const { setGlobalLoading, showToast, hideToast } = uiSlice.actions;
export default uiSlice.reducer;
