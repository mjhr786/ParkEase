/**
 * Vehicle Slice
 * State for user's saved vehicles
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getVehiclesThunk = createAsyncThunk(
    'vehicle/getAll',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.VEHICLES.BASE);
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const addVehicleThunk = createAsyncThunk(
    'vehicle/add',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.VEHICLES.BASE, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    vehicles: [],
    loading: false,
    addLoading: false,
    error: null,
};

const vehicleSlice = createSlice({
    name: 'vehicle',
    initialState,
    reducers: {
        clearVehicles: () => initialState,
    },
    extraReducers: (builder) => {
        builder
            .addCase(getVehiclesThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(getVehiclesThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.vehicles = action.payload?.vehicles || action.payload || [];
            })
            .addCase(getVehiclesThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(addVehicleThunk.pending, (state) => {
                state.addLoading = true;
            })
            .addCase(addVehicleThunk.fulfilled, (state, action) => {
                state.addLoading = false;
                if (action.payload) {
                    state.vehicles = [action.payload, ...state.vehicles];
                }
            })
            .addCase(addVehicleThunk.rejected, (state) => {
                state.addLoading = false;
            });
    },
});

export const { clearVehicles } = vehicleSlice.actions;
export default vehicleSlice.reducer;
