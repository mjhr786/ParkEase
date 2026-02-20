/**
 * Dashboard Slice
 * Member and vendor dashboard data
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getMemberDashboardThunk = createAsyncThunk(
    'dashboard/getMember',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.DASHBOARD.MEMBER);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getVendorDashboardThunk = createAsyncThunk(
    'dashboard/getVendor',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.DASHBOARD.VENDOR);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    memberDashboard: null,
    vendorDashboard: null,
    loading: false,
    error: null,
};

const dashboardSlice = createSlice({
    name: 'dashboard',
    initialState,
    reducers: {
        clearDashboard: () => initialState,
    },
    extraReducers: (builder) => {
        builder
            .addCase(getMemberDashboardThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(getMemberDashboardThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.memberDashboard = action.payload;
            })
            .addCase(getMemberDashboardThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(getVendorDashboardThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(getVendorDashboardThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.vendorDashboard = action.payload;
            })
            .addCase(getVendorDashboardThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            });
    },
});

export const { clearDashboard } = dashboardSlice.actions;
export default dashboardSlice.reducer;
