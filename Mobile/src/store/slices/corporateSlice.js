import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import corporateService from '../../services/api/corporateService';

// Async Thunks
export const fetchMyCompanies = createAsyncThunk(
    'corporate/fetchMyCompanies',
    async (_, { rejectWithValue }) => {
        try {
            const response = await corporateService.getMyCompanies();
            return response.data;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch companies');
        }
    }
);

export const fetchCompanyDetails = createAsyncThunk(
    'corporate/fetchCompanyDetails',
    async (companyId, { rejectWithValue }) => {
        try {
            const response = await corporateService.getCompanyDetails(companyId);
            return response.data;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch company details');
        }
    }
);

const initialState = {
    myCompanies: [],
    activeCompanyId: null,
    activeCompanyDetails: null,
    isLoading: false,
    error: null,
};

const corporateSlice = createSlice({
    name: 'corporate',
    initialState,
    reducers: {
        setActiveCompany: (state, action) => {
            state.activeCompanyId = action.payload;
            state.activeCompanyDetails = null; // Reset details when switching
        },
        clearCorporateState: (state) => {
            state.myCompanies = [];
            state.activeCompanyId = null;
            state.activeCompanyDetails = null;
            state.error = null;
        },
    },
    extraReducers: (builder) => {
        builder
            // Fetch My Companies
            .addCase(fetchMyCompanies.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchMyCompanies.fulfilled, (state, action) => {
                state.isLoading = false;
                state.myCompanies = action.payload || [];
                // If there's no active company but we have companies, default to the first one
                if (!state.activeCompanyId && state.myCompanies.length > 0) {
                    state.activeCompanyId = state.myCompanies[0].id;
                }
            })
            .addCase(fetchMyCompanies.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            })
            // Fetch Company Details
            .addCase(fetchCompanyDetails.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchCompanyDetails.fulfilled, (state, action) => {
                state.isLoading = false;
                state.activeCompanyDetails = action.payload;
            })
            .addCase(fetchCompanyDetails.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            });
    },
});

export const { setActiveCompany, clearCorporateState } = corporateSlice.actions;

export default corporateSlice.reducer;
