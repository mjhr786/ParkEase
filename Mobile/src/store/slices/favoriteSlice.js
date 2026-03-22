/**
 * Favorite Slice
 * State for user's favorited parking spaces
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getFavoritesThunk = createAsyncThunk(
    'favorite/getAll',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.FAVORITES.BASE);
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const toggleFavoriteThunk = createAsyncThunk(
    'favorite/toggle',
    async (parkingSpaceId, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.FAVORITES.TOGGLE(parkingSpaceId));
            return { parkingSpaceId, ...(response.data.data || response.data) };
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    favorites: [],
    loading: false,
    toggleLoading: false,
    error: null,
};

const favoriteSlice = createSlice({
    name: 'favorite',
    initialState,
    reducers: {
        clearFavorites: () => initialState,
    },
    extraReducers: (builder) => {
        builder
            .addCase(getFavoritesThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(getFavoritesThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.favorites = action.payload?.favorites || action.payload || [];
            })
            .addCase(getFavoritesThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(toggleFavoriteThunk.pending, (state) => {
                state.toggleLoading = true;
            })
            .addCase(toggleFavoriteThunk.fulfilled, (state, action) => {
                state.toggleLoading = false;
                const { parkingSpaceId, isFavorited } = action.payload;
                if (isFavorited === false) {
                    state.favorites = state.favorites.filter(
                        (f) => (f.parkingSpaceId || f.id) !== parkingSpaceId
                    );
                }
            })
            .addCase(toggleFavoriteThunk.rejected, (state) => {
                state.toggleLoading = false;
            });
    },
});

export const { clearFavorites } = favoriteSlice.actions;
export default favoriteSlice.reducer;
