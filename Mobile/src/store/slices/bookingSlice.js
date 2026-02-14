/**
 * Booking Slice
 * State for user bookings, vendor bookings, and booking operations
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getMyBookingsThunk = createAsyncThunk(
    'booking/getMyBookings',
    async (params = {}, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.BOOKINGS.MY_BOOKINGS, { params });
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getBookingDetailThunk = createAsyncThunk(
    'booking/getDetail',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.BOOKINGS.BY_ID(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const createBookingThunk = createAsyncThunk(
    'booking/create',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.BASE, data);
            if (!response.data.success) {
                return rejectWithValue(response.data.message);
            }
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const cancelBookingThunk = createAsyncThunk(
    'booking/cancel',
    async ({ id, reason }, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.CANCEL(id), { reason });
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const calculatePriceThunk = createAsyncThunk(
    'booking/calculatePrice',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.CALCULATE_PRICE, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

// Vendor actions
export const getVendorBookingsThunk = createAsyncThunk(
    'booking/getVendorBookings',
    async (params = {}, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.BOOKINGS.VENDOR_BOOKINGS, { params });
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const approveBookingThunk = createAsyncThunk(
    'booking/approve',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.APPROVE(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const rejectBookingThunk = createAsyncThunk(
    'booking/reject',
    async ({ id, reason }, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.REJECT(id), { reason });
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    myBookings: [],
    myBookingsLoading: false,
    myBookingsError: null,
    vendorBookings: [],
    vendorBookingsLoading: false,
    selectedBooking: null,
    detailLoading: false,
    createLoading: false,
    priceBreakdown: null,
    priceLoading: false,
    actionLoading: false,
};

const bookingSlice = createSlice({
    name: 'booking',
    initialState,
    reducers: {
        clearBookingDetail: (state) => {
            state.selectedBooking = null;
        },
        clearPriceBreakdown: (state) => {
            state.priceBreakdown = null;
        },
    },
    extraReducers: (builder) => {
        builder
            // My Bookings
            .addCase(getMyBookingsThunk.pending, (state) => {
                state.myBookingsLoading = true;
                state.myBookingsError = null;
            })
            .addCase(getMyBookingsThunk.fulfilled, (state, action) => {
                state.myBookingsLoading = false;
                state.myBookings = action.payload?.bookings || action.payload || [];
            })
            .addCase(getMyBookingsThunk.rejected, (state, action) => {
                state.myBookingsLoading = false;
                state.myBookingsError = action.payload;
            })
            // Booking Detail
            .addCase(getBookingDetailThunk.pending, (state) => {
                state.detailLoading = true;
            })
            .addCase(getBookingDetailThunk.fulfilled, (state, action) => {
                state.detailLoading = false;
                state.selectedBooking = action.payload;
            })
            .addCase(getBookingDetailThunk.rejected, (state) => {
                state.detailLoading = false;
            })
            // Create Booking
            .addCase(createBookingThunk.pending, (state) => {
                state.createLoading = true;
            })
            .addCase(createBookingThunk.fulfilled, (state, action) => {
                state.createLoading = false;
                if (action.payload) {
                    state.myBookings = [action.payload, ...state.myBookings];
                }
            })
            .addCase(createBookingThunk.rejected, (state) => {
                state.createLoading = false;
            })
            // Cancel Booking
            .addCase(cancelBookingThunk.fulfilled, (state, action) => {
                if (action.payload) {
                    const idx = state.myBookings.findIndex((b) => b.id === action.payload.id);
                    if (idx !== -1) state.myBookings[idx] = action.payload;
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            // Calculate Price
            .addCase(calculatePriceThunk.pending, (state) => {
                state.priceLoading = true;
            })
            .addCase(calculatePriceThunk.fulfilled, (state, action) => {
                state.priceLoading = false;
                state.priceBreakdown = action.payload;
            })
            .addCase(calculatePriceThunk.rejected, (state) => {
                state.priceLoading = false;
            })
            // Vendor Bookings
            .addCase(getVendorBookingsThunk.pending, (state) => {
                state.vendorBookingsLoading = true;
            })
            .addCase(getVendorBookingsThunk.fulfilled, (state, action) => {
                state.vendorBookingsLoading = false;
                state.vendorBookings = action.payload?.bookings || action.payload || [];
            })
            .addCase(getVendorBookingsThunk.rejected, (state) => {
                state.vendorBookingsLoading = false;
            })
            // Approve
            .addCase(approveBookingThunk.fulfilled, (state, action) => {
                if (action.payload) {
                    const idx = state.vendorBookings.findIndex((b) => b.id === action.payload.id);
                    if (idx !== -1) state.vendorBookings[idx] = action.payload;
                }
            })
            // Reject
            .addCase(rejectBookingThunk.fulfilled, (state, action) => {
                if (action.payload) {
                    const idx = state.vendorBookings.findIndex((b) => b.id === action.payload.id);
                    if (idx !== -1) state.vendorBookings[idx] = action.payload;
                }
            });
    },
});

export const { clearBookingDetail, clearPriceBreakdown } = bookingSlice.actions;
export default bookingSlice.reducer;
