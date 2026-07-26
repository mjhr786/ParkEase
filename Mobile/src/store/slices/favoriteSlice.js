import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import { ENDPOINTS } from '../../services/api/endpoints';

export const toggleFavoriteThunk = createAsyncThunk(
    'favorites/toggle',
    async (parkingSpaceId, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.FAVORITES.TOGGLE(parkingSpaceId));
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(error?.response?.data?.message || 'Failed to toggle favorite');
        }
    }
);

const favoriteSlice = createSlice({
    name: 'favorite',
    initialState: {
        favorites: [],
        isLoading: false,
        error: null
    },
    reducers: {},
    extraReducers: (builder) => {
        builder
            .addCase(toggleFavoriteThunk.pending, (state) => {
                state.isLoading = true;
            })
            .addCase(toggleFavoriteThunk.fulfilled, (state, action) => {
                state.isLoading = false;
                // Basic implementation
            })
            .addCase(toggleFavoriteThunk.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            });
    }
});

export default favoriteSlice.reducer;
