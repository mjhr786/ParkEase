/**
 * Parking Slice
 * State for parking search, listings, and parking details
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const searchParkingThunk = createAsyncThunk(
    'parking/search',
    async (params, { rejectWithValue }) => {
        try {
            const cleanParams = Object.fromEntries(
                Object.entries(params).filter(([, v]) => v != null && v !== '')
            );
            const response = await apiClient.get(ENDPOINTS.PARKING.SEARCH, { params: cleanParams });
            return response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getParkingDetailThunk = createAsyncThunk(
    'parking/getDetail',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.PARKING.BY_ID(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getMyListingsThunk = createAsyncThunk(
    'parking/getMyListings',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.PARKING.MY_LISTINGS);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const createParkingThunk = createAsyncThunk(
    'parking/create',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.PARKING.BASE, data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const updateParkingThunk = createAsyncThunk(
    'parking/update',
    async ({ id, data }, { rejectWithValue }) => {
        try {
            const response = await apiClient.put(ENDPOINTS.PARKING.BY_ID(id), data);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const toggleParkingActiveThunk = createAsyncThunk(
    'parking/toggleActive',
    async (id, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.PARKING.TOGGLE_ACTIVE(id));
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const deleteParkingThunk = createAsyncThunk(
    'parking/delete',
    async (id, { rejectWithValue }) => {
        try {
            await apiClient.delete(ENDPOINTS.PARKING.BY_ID(id));
            return id;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const uploadParkingImagesThunk = createAsyncThunk(
    'parking/uploadImages',
    async ({ parkingSpaceId, images }, { rejectWithValue }) => {
        try {
            const formData = new FormData();
            images.forEach((image, index) => {
                formData.append('files', {
                    uri: image.uri,
                    type: image.type || 'image/jpeg',
                    name: image.fileName || `image_${index}.jpg`,
                });
            });
            const response = await apiClient.post(
                ENDPOINTS.FILES.UPLOAD(parkingSpaceId),
                formData,
                { headers: { 'Content-Type': 'multipart/form-data' } }
            );
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getMapCoordinatesThunk = createAsyncThunk(
    'parking/getMapCoordinates',
    async (params = {}, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.PARKING.MAP, { params });
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    searchResults: [],
    searchTotalCount: 0,
    searchPage: 1,
    searchLoading: false,
    searchError: null,
    selectedParking: null,
    detailLoading: false,
    myListings: [],
    listingsLoading: false,
    createLoading: false,
    uploadLoading: false,
    mapCoordinates: [],
    mapLoading: false,
};

const parkingSlice = createSlice({
    name: 'parking',
    initialState,
    reducers: {
        clearSearch: (state) => {
            state.searchResults = [];
            state.searchTotalCount = 0;
            state.searchError = null;
        },
        clearSelectedParking: (state) => {
            state.selectedParking = null;
        },
    },
    extraReducers: (builder) => {
        builder
            .addCase(searchParkingThunk.pending, (state) => {
                state.searchLoading = true;
                state.searchError = null;
            })
            .addCase(searchParkingThunk.fulfilled, (state, action) => {
                state.searchLoading = false;
                const data = action.payload.data || action.payload;
                state.searchResults = data.parkingSpaces || [];
                state.searchTotalCount = data.totalCount || 0;
            })
            .addCase(searchParkingThunk.rejected, (state, action) => {
                state.searchLoading = false;
                state.searchError = action.payload;
            })
            .addCase(getParkingDetailThunk.pending, (state) => {
                state.detailLoading = true;
            })
            .addCase(getParkingDetailThunk.fulfilled, (state, action) => {
                state.detailLoading = false;
                state.selectedParking = action.payload;
            })
            .addCase(getParkingDetailThunk.rejected, (state) => {
                state.detailLoading = false;
            })
            .addCase(getMyListingsThunk.pending, (state) => {
                state.listingsLoading = true;
            })
            .addCase(getMyListingsThunk.fulfilled, (state, action) => {
                state.listingsLoading = false;
                state.myListings = action.payload || [];
            })
            .addCase(getMyListingsThunk.rejected, (state) => {
                state.listingsLoading = false;
            })
            .addCase(createParkingThunk.pending, (state) => {
                state.createLoading = true;
            })
            .addCase(createParkingThunk.fulfilled, (state, action) => {
                state.createLoading = false;
                if (action.payload) {
                    state.myListings = [action.payload, ...state.myListings];
                }
            })
            .addCase(createParkingThunk.rejected, (state) => {
                state.createLoading = false;
            })
            .addCase(toggleParkingActiveThunk.fulfilled, (state, action) => {
                if (action.payload) {
                    const idx = state.myListings.findIndex((l) => l.id === action.payload.id);
                    if (idx !== -1) state.myListings[idx] = action.payload;
                }
            })
            // Delete Parking
            .addCase(deleteParkingThunk.fulfilled, (state, action) => {
                state.myListings = state.myListings.filter((l) => l.id !== action.payload);
            })
            // Upload Images
            .addCase(uploadParkingImagesThunk.pending, (state) => {
                state.uploadLoading = true;
            })
            .addCase(uploadParkingImagesThunk.fulfilled, (state) => {
                state.uploadLoading = false;
            })
            .addCase(uploadParkingImagesThunk.rejected, (state) => {
                state.uploadLoading = false;
            })
            // Map Coordinates
            .addCase(getMapCoordinatesThunk.pending, (state) => {
                state.mapLoading = true;
            })
            .addCase(getMapCoordinatesThunk.fulfilled, (state, action) => {
                state.mapLoading = false;
                state.mapCoordinates = action.payload || [];
            })
            .addCase(getMapCoordinatesThunk.rejected, (state) => {
                state.mapLoading = false;
            });
    },
});

export const { clearSearch, clearSelectedParking } = parkingSlice.actions;
export default parkingSlice.reducer;
