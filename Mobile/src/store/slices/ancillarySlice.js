import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import ancillaryService from '../../services/api/ancillaryService';

export const fetchServicesByParking = createAsyncThunk(
    'ancillary/fetchByParking',
    async (parkingSpaceId, { rejectWithValue }) => {
        try {
            const response = await ancillaryService.getByParking(parkingSpaceId);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch ancillary services');
        }
    }
);

export const fetchMyServices = createAsyncThunk(
    'ancillary/fetchMyServices',
    async (_, { rejectWithValue }) => {
        try {
            const response = await ancillaryService.getMyServices();
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch my services');
        }
    }
);

export const createAncillaryService = createAsyncThunk(
    'ancillary/create',
    async (serviceData, { rejectWithValue }) => {
        try {
            const response = await ancillaryService.createService(serviceData);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to create service');
        }
    }
);

export const updateAncillaryService = createAsyncThunk(
    'ancillary/update',
    async ({ id, updateData }, { rejectWithValue }) => {
        try {
            const response = await ancillaryService.updateService(id, updateData);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to update service');
        }
    }
);

export const deactivateAncillaryService = createAsyncThunk(
    'ancillary/deactivate',
    async (id, { rejectWithValue }) => {
        try {
            const response = await ancillaryService.deactivateService(id);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to deactivate service');
        }
    }
);

const ancillarySlice = createSlice({
    name: 'ancillary',
    initialState: {
        services: [],
        myServices: [],
        isLoading: false,
        error: null,
    },
    reducers: {
        clearAncillaryState: (state) => {
            state.services = [];
            state.myServices = [];
            state.error = null;
        }
    },
    extraReducers: (builder) => {
        builder
            .addCase(fetchServicesByParking.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchServicesByParking.fulfilled, (state, action) => {
                state.isLoading = false;
                state.services = action.payload || [];
            })
            .addCase(fetchServicesByParking.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            })
            .addCase(fetchMyServices.pending, (state) => {
                state.isLoading = true;
            })
            .addCase(fetchMyServices.fulfilled, (state, action) => {
                state.isLoading = false;
                state.myServices = action.payload || [];
            })
            .addCase(fetchMyServices.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            });
    }
});

export const { clearAncillaryState } = ancillarySlice.actions;
export default ancillarySlice.reducer;
