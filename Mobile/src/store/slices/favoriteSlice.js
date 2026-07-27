import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import favoriteService from '../../services/api/favoriteService';

export const fetchFavoritesThunk = createAsyncThunk(
    'favorites/fetch',
    async (_, { rejectWithValue }) => {
        try {
            const response = await favoriteService.getFavorites();
            if (response.success) {
                return response.data;
            }
            return rejectWithValue(response.message || 'Failed to fetch favorites');
        } catch (error) {
            return rejectWithValue(error.message);
        }
    }
);

export const toggleFavoriteThunk = createAsyncThunk(
    'favorites/toggle',
    async (parkingSpaceId, { rejectWithValue }) => {
        try {
            const response = await favoriteService.toggleFavorite(parkingSpaceId);
            if (response.success) {
                // The backend usually returns the updated status or we just flip it locally
                return { parkingSpaceId, isFavorite: response.data.isFavorite };
            }
            return rejectWithValue(response.message || 'Failed to toggle favorite');
        } catch (error) {
            return rejectWithValue(error.message);
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
            // Fetch Favorites
            .addCase(fetchFavoritesThunk.pending, (state) => {
                state.isLoading = true;
                state.error = null;
            })
            .addCase(fetchFavoritesThunk.fulfilled, (state, action) => {
                state.isLoading = false;
                state.favorites = action.payload || [];
            })
            .addCase(fetchFavoritesThunk.rejected, (state, action) => {
                state.isLoading = false;
                state.error = action.payload;
            })
            // Toggle Favorite
            .addCase(toggleFavoriteThunk.fulfilled, (state, action) => {
                const { parkingSpaceId, isFavorite } = action.payload;
                if (isFavorite) {
                    // It was added (we don't have the full object here, so we might need to fetch again
                    // but for now we just remove it if it was unfavorited)
                } else {
                    state.favorites = state.favorites.filter(f => f.id !== parkingSpaceId);
                }
            });
    }
});

export default favoriteSlice.reducer;
