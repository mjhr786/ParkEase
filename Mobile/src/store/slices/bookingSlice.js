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

export const updateBookingThunk = createAsyncThunk(
    'booking/update',
    async ({ id, data }, { rejectWithValue }) => {
        try {
            const response = await apiClient.put(ENDPOINTS.BOOKINGS.BY_ID(id), data);
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

export const checkInBookingThunk = createAsyncThunk(
    'booking/checkIn',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.CHECK_IN(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const checkOutBookingThunk = createAsyncThunk(
    'booking/checkOut',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.CHECK_OUT(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const extendBookingThunk = createAsyncThunk(
    'booking/extend',
    async ({ id, data }, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.EXTEND(id), data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const approveExtensionThunk = createAsyncThunk(
    'booking/approveExtension',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.APPROVE_EXTENSION(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const rejectExtensionThunk = createAsyncThunk(
    'booking/rejectExtension',
    async ({ id, reason }, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.BOOKINGS.REJECT_EXTENSION(id), { reason });
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getPendingCountThunk = createAsyncThunk(
    'booking/getPendingCount',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.BOOKINGS.PENDING_COUNT);
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
    extensionLoading: false,
    pendingCount: 0,
};

const updateBookingInList = (list, updatedBooking) => {
    if (!updatedBooking) return list;
    const idx = list.findIndex((b) => b.id === updatedBooking.id);
    if (idx !== -1) {
        list[idx] = updatedBooking;
    }
    return list;
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
            // Update Booking
            .addCase(updateBookingThunk.fulfilled, (state, action) => {
                if (action.payload) {
                    updateBookingInList(state.myBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            // Cancel Booking
            .addCase(cancelBookingThunk.fulfilled, (state, action) => {
                if (action.payload) {
                    updateBookingInList(state.myBookings, action.payload);
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
            // Check In
            .addCase(checkInBookingThunk.pending, (state) => {
                state.actionLoading = true;
            })
            .addCase(checkInBookingThunk.fulfilled, (state, action) => {
                state.actionLoading = false;
                if (action.payload) {
                    updateBookingInList(state.myBookings, action.payload);
                    updateBookingInList(state.vendorBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            .addCase(checkInBookingThunk.rejected, (state) => {
                state.actionLoading = false;
            })
            // Check Out
            .addCase(checkOutBookingThunk.pending, (state) => {
                state.actionLoading = true;
            })
            .addCase(checkOutBookingThunk.fulfilled, (state, action) => {
                state.actionLoading = false;
                if (action.payload) {
                    updateBookingInList(state.myBookings, action.payload);
                    updateBookingInList(state.vendorBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            .addCase(checkOutBookingThunk.rejected, (state) => {
                state.actionLoading = false;
            })
            // Extend
            .addCase(extendBookingThunk.pending, (state) => {
                state.extensionLoading = true;
            })
            .addCase(extendBookingThunk.fulfilled, (state, action) => {
                state.extensionLoading = false;
                if (action.payload) {
                    updateBookingInList(state.myBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            .addCase(extendBookingThunk.rejected, (state) => {
                state.extensionLoading = false;
            })
            // Approve Extension
            .addCase(approveExtensionThunk.pending, (state) => {
                state.extensionLoading = true;
            })
            .addCase(approveExtensionThunk.fulfilled, (state, action) => {
                state.extensionLoading = false;
                if (action.payload) {
                    updateBookingInList(state.vendorBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            .addCase(approveExtensionThunk.rejected, (state) => {
                state.extensionLoading = false;
            })
            // Reject Extension
            .addCase(rejectExtensionThunk.pending, (state) => {
                state.extensionLoading = true;
            })
            .addCase(rejectExtensionThunk.fulfilled, (state, action) => {
                state.extensionLoading = false;
                if (action.payload) {
                    updateBookingInList(state.vendorBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            .addCase(rejectExtensionThunk.rejected, (state) => {
                state.extensionLoading = false;
            })
            // Pending Count
            .addCase(getPendingCountThunk.fulfilled, (state, action) => {
                state.pendingCount = action.payload?.count ?? action.payload ?? 0;
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
            .addCase(approveBookingThunk.pending, (state) => {
                state.actionLoading = true;
            })
            .addCase(approveBookingThunk.fulfilled, (state, action) => {
                state.actionLoading = false;
                if (action.payload) {
                    updateBookingInList(state.vendorBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            .addCase(approveBookingThunk.rejected, (state) => {
                state.actionLoading = false;
            })
            // Reject
            .addCase(rejectBookingThunk.pending, (state) => {
                state.actionLoading = true;
            })
            .addCase(rejectBookingThunk.fulfilled, (state, action) => {
                state.actionLoading = false;
                if (action.payload) {
                    updateBookingInList(state.vendorBookings, action.payload);
                    if (state.selectedBooking?.id === action.payload.id) {
                        state.selectedBooking = action.payload;
                    }
                }
            })
            .addCase(rejectBookingThunk.rejected, (state) => {
                state.actionLoading = false;
            });
    },
});

export const { clearBookingDetail, clearPriceBreakdown } = bookingSlice.actions;
export default bookingSlice.reducer;
