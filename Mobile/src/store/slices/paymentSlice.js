/**
 * Payment Slice
 * State for payment processing, verification, and refunds
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getStripeConfigThunk = createAsyncThunk(
    'payment/getStripeConfig',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.PAYMENTS.STRIPE_CONFIG);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const processPaymentThunk = createAsyncThunk(
    'payment/process',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.PAYMENTS.BASE, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const verifyPaymentThunk = createAsyncThunk(
    'payment/verify',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.PAYMENTS.VERIFY, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getPaymentStatusThunk = createAsyncThunk(
    'payment/getStatus',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.PAYMENTS.BY_ID(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const processRefundThunk = createAsyncThunk(
    'payment/refund',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.PAYMENTS.REFUND, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    stripeConfig: null,
    paymentResult: null,
    paymentStatus: null,
    loading: false,
    verifyLoading: false,
    refundLoading: false,
    error: null,
};

const paymentSlice = createSlice({
    name: 'payment',
    initialState,
    reducers: {
        clearPayment: () => initialState,
        clearPaymentResult: (state) => {
            state.paymentResult = null;
            state.error = null;
        },
    },
    extraReducers: (builder) => {
        builder
            .addCase(getStripeConfigThunk.fulfilled, (state, action) => {
                state.stripeConfig = action.payload;
            })
            .addCase(processPaymentThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(processPaymentThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.paymentResult = action.payload;
            })
            .addCase(processPaymentThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(verifyPaymentThunk.pending, (state) => {
                state.verifyLoading = true;
            })
            .addCase(verifyPaymentThunk.fulfilled, (state, action) => {
                state.verifyLoading = false;
                state.paymentResult = action.payload;
            })
            .addCase(verifyPaymentThunk.rejected, (state) => {
                state.verifyLoading = false;
            })
            .addCase(getPaymentStatusThunk.fulfilled, (state, action) => {
                state.paymentStatus = action.payload;
            })
            .addCase(processRefundThunk.pending, (state) => {
                state.refundLoading = true;
            })
            .addCase(processRefundThunk.fulfilled, (state, action) => {
                state.refundLoading = false;
                state.paymentResult = action.payload;
            })
            .addCase(processRefundThunk.rejected, (state) => {
                state.refundLoading = false;
            });
    },
});

export const { clearPayment, clearPaymentResult } = paymentSlice.actions;
export default paymentSlice.reducer;
