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

export const updateVehicleThunk = createAsyncThunk(
    'vehicle/update',
    async ({ id, data }, { rejectWithValue }) => {
        try {
            const response = await apiClient.put(`${ENDPOINTS.VEHICLES.BASE}/${id}`, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const deleteVehicleThunk = createAsyncThunk(
    'vehicle/delete',
    async (id, { rejectWithValue }) => {
        try {
            await apiClient.delete(`${ENDPOINTS.VEHICLES.BASE}/${id}`);
            return id;
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
            })
            .addCase(updateVehicleThunk.pending, (state) => {
                state.addLoading = true;
            })
            .addCase(updateVehicleThunk.fulfilled, (state, action) => {
                state.addLoading = false;
                if (action.payload) {
                    const index = state.vehicles.findIndex(v => v.id === action.payload.id);
                    if (index !== -1) {
                        state.vehicles[index] = action.payload;
                    }
                }
            })
            .addCase(updateVehicleThunk.rejected, (state) => {
                state.addLoading = false;
            })
            .addCase(deleteVehicleThunk.pending, (state) => {
                state.loading = true;
            })
            .addCase(deleteVehicleThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.vehicles = state.vehicles.filter(v => v.id !== action.payload);
            })
            .addCase(deleteVehicleThunk.rejected, (state) => {
                state.loading = false;
            });
    },
});

export const { clearVehicles } = vehicleSlice.actions;
export default vehicleSlice.reducer;
