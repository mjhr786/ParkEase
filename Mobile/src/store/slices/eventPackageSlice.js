import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import eventPackageService from '../../services/api/eventPackageService';

export const fetchOnSalePackages = createAsyncThunk(
    'eventPackage/fetchOnSale',
    async (params, { rejectWithValue }) => {
        try {
            const response = await eventPackageService.getOnSale(params);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch on-sale packages');
        }
    }
);

export const fetchMyEventPackages = createAsyncThunk(
    'eventPackage/fetchMyPackages',
    async (_, { rejectWithValue }) => {
        try {
            const response = await eventPackageService.getMyPackages();
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch your packages');
        }
    }
);

export const purchaseEventPackage = createAsyncThunk(
    'eventPackage/purchase',
    async ({ id, purchaseData }, { rejectWithValue }) => {
        try {
            const response = await eventPackageService.purchasePackage(id, purchaseData);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to purchase package');
        }
    }
);

const eventPackageSlice = createSlice({
    name: 'eventPackage',
    initialState: {
        onSalePackages: [],
        myPackages: [],
        isLoading: false,
        error: null,
    },
    reducers: {
        clearEventPackageState: (state) => {
            state.onSalePackages = [];
            state.myPackages = [];
            state.error = null;
        }
    },
    extraReducers: (builder) => {
        builder
            .addCase(fetchOnSalePackages.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchOnSalePackages.fulfilled, (state, action) => {
                state.isLoading = false;
                state.onSalePackages = action.payload || [];
            })
            .addCase(fetchOnSalePackages.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            })
            .addCase(fetchMyEventPackages.pending, (state) => {
                state.isLoading = true;
            })
            .addCase(fetchMyEventPackages.fulfilled, (state, action) => {
                state.isLoading = false;
                state.myPackages = action.payload || [];
            })
            .addCase(fetchMyEventPackages.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            });
    }
});

export const { clearEventPackageState } = eventPackageSlice.actions;
export default eventPackageSlice.reducer;
