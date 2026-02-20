/**
 * Review Slice
 * Reviews for parking spaces
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getReviewsThunk = createAsyncThunk(
    'review/getByParkingSpace',
    async (parkingSpaceId, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.REVIEWS.BY_PARKING_SPACE(parkingSpaceId));
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const createReviewThunk = createAsyncThunk(
    'review/create',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.REVIEWS.BASE, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    reviews: [],
    loading: false,
    createLoading: false,
    error: null,
};

const reviewSlice = createSlice({
    name: 'review',
    initialState,
    reducers: {
        clearReviews: () => initialState,
    },
    extraReducers: (builder) => {
        builder
            .addCase(getReviewsThunk.pending, (state) => {
                state.loading = true;
            })
            .addCase(getReviewsThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.reviews = action.payload || [];
            })
            .addCase(getReviewsThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(createReviewThunk.pending, (state) => {
                state.createLoading = true;
            })
            .addCase(createReviewThunk.fulfilled, (state, action) => {
                state.createLoading = false;
                if (action.payload) {
                    state.reviews = [action.payload, ...state.reviews];
                }
            })
            .addCase(createReviewThunk.rejected, (state) => {
                state.createLoading = false;
            });
    },
});

export const { clearReviews } = reviewSlice.actions;
export default reviewSlice.reducer;
