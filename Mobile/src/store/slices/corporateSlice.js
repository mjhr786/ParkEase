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

// Invoices
export const getInvoicesThunk = createAsyncThunk(
    'corporate/getInvoices',
    async ({ companyId, params }, { rejectWithValue }) => {
        try {
            const response = await corporateService.getInvoices(companyId, params);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch invoices');
        }
    }
);

export const getInvoiceDetailsThunk = createAsyncThunk(
    'corporate/getInvoiceDetails',
    async ({ companyId, invoiceId }, { rejectWithValue }) => {
        try {
            const response = await corporateService.getInvoiceDetails(companyId, invoiceId);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to fetch invoice details');
        }
    }
);

export const generateInvoicesThunk = createAsyncThunk(
    'corporate/generateInvoices',
    async ({ companyId, periodData }, { rejectWithValue }) => {
        try {
            const response = await corporateService.generateInvoices(companyId, periodData);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to generate invoices');
        }
    }
);

export const issueInvoiceThunk = createAsyncThunk(
    'corporate/issueInvoice',
    async ({ companyId, invoiceId }, { rejectWithValue }) => {
        try {
            const response = await corporateService.issueInvoice(companyId, invoiceId);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to issue invoice');
        }
    }
);

export const markInvoicePaidThunk = createAsyncThunk(
    'corporate/markInvoicePaid',
    async ({ companyId, invoiceId, paymentData }, { rejectWithValue }) => {
        try {
            const response = await corporateService.markInvoicePaid(companyId, invoiceId, paymentData);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to mark invoice as paid');
        }
    }
);

export const voidInvoiceThunk = createAsyncThunk(
    'corporate/voidInvoice',
    async ({ companyId, invoiceId, reasonData }, { rejectWithValue }) => {
        try {
            const response = await corporateService.voidInvoice(companyId, invoiceId, reasonData);
            return response.data || response;
        } catch (error) {
            return rejectWithValue(error.response?.data?.message || 'Failed to void invoice');
        }
    }
);

const initialState = {
    myCompanies: [],
    activeCompanyId: null,
    activeCompanyDetails: null,
    isLoading: false,
    error: null,
    invoices: [],
    selectedInvoice: null,
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
            })
            // Invoices
            .addCase(getInvoicesThunk.fulfilled, (state, action) => {
                state.invoices = action.payload?.items || action.payload || [];
            })
            .addCase(getInvoiceDetailsThunk.fulfilled, (state, action) => {
                state.selectedInvoice = action.payload;
            })
            .addMatcher(
                (action) => [
                    issueInvoiceThunk.fulfilled.type,
                    markInvoicePaidThunk.fulfilled.type,
                    voidInvoiceThunk.fulfilled.type,
                ].includes(action.type),
                (state, action) => {
                    if (action.payload) {
                        const updated = action.payload;
                        if (state.selectedInvoice?.id === updated.id) {
                            state.selectedInvoice = updated;
                        }
                        const idx = state.invoices.findIndex(i => i.id === updated.id);
                        if (idx !== -1) {
                            state.invoices[idx] = updated;
                        }
                    }
                }
            );
    },
});

export const { setActiveCompany, clearCorporateState } = corporateSlice.actions;

export default corporateSlice.reducer;
