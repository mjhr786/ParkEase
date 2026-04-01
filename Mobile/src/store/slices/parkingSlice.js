/**
 * Parking Slice
 * State for parking search, listings, and parking details
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;
const ALLOWED_IMAGE_TYPES = new Set(['image/jpeg', 'image/jpg', 'image/png', 'image/webp']);

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
    async ({ parkingSpaceId, images = [], existingImageUrls = [], callbacks = {} }, { rejectWithValue }) => {
        const completedUploads = [];

        try {
            const {
                onFileStart,
                onProgress,
                onFileComplete,
                onFileError,
            } = callbacks;

            for (let index = 0; index < images.length; index += 1) {
                const image = images[index];

                try {
                    const contentType = getImageContentType(image);
                    const fileName = image.fileName || `image_${index}${getExtensionFromContentType(contentType)}`;

                    validateImageForUpload(image, contentType, fileName);
                    onFileStart?.(image, { index, fileName, contentType });

                    const signResponse = await apiClient.post(
                        ENDPOINTS.FILES.SIGN_UPLOAD(parkingSpaceId),
                        { fileName, contentType }
                    );

                    const signData = signResponse.data?.data || signResponse.data;
                    if (!signData?.uploadUrl || !signData?.publicUrl) {
                        throw new Error('Upload URL generation failed.');
                    }

                    const localFileResponse = await fetch(image.uri);
                    const fileBlob = await localFileResponse.blob();
                    await uploadFileToPresignedUrl({
                        uploadUrl: signData.uploadUrl,
                        contentType,
                        fileBlob,
                        onProgress: (progress) => onProgress?.(image, progress, { index, fileName }),
                    });

                    completedUploads.push({
                        image,
                        index,
                        fileName,
                        publicUrl: signData.publicUrl,
                    });
                } catch (error) {
                    onFileError?.({ image, index, error });
                    throw error;
                }
            }

            const publicUrls = [
                ...sanitizePublicUrls(existingImageUrls),
                ...completedUploads.map((item) => item.publicUrl),
            ];
            const confirmResponse = await apiClient.post(
                ENDPOINTS.FILES.CONFIRM_UPLOAD(parkingSpaceId),
                { fileUrls: publicUrls }
            );

            completedUploads.forEach(({ image, index, fileName, publicUrl }) => {
                onFileComplete?.(image, { index, fileName, publicUrl });
            });

            return confirmResponse.data?.data || { publicUrls };
        } catch (error) {
            const normalizedError = error instanceof Error ? error : new Error(getErrorMessage(error));
            completedUploads.forEach(({ image, index }) => {
                onFileError?.({ image, index, error: normalizedError });
            });
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const uploadFileToPresignedUrl = ({ uploadUrl, contentType, fileBlob, onProgress }) =>
    new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();

        xhr.open('PUT', uploadUrl);
        xhr.setRequestHeader('Content-Type', contentType);

        xhr.upload.onprogress = (event) => {
            if (!event.lengthComputable) {
                return;
            }

            const progress = Math.min(100, Math.round((event.loaded / event.total) * 100));
            onProgress?.(progress);
        };

        xhr.onload = () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                onProgress?.(100);
                resolve();
                return;
            }

            reject(new Error(`Direct upload failed with status ${xhr.status}.`));
        };

        xhr.onerror = () => reject(new Error('Direct upload failed.'));
        xhr.onabort = () => reject(new Error('Direct upload was cancelled.'));
        xhr.send(fileBlob);
    });

const getImageContentType = (image) => {
    const explicitType = image.mimeType || image.type;
    if (explicitType && explicitType.includes('/')) {
        return explicitType.toLowerCase();
    }

    const fileName = (image.fileName || image.uri || '').toLowerCase();
    if (fileName.endsWith('.png')) return 'image/png';
    if (fileName.endsWith('.webp')) return 'image/webp';
    return 'image/jpeg';
};

const getExtensionFromContentType = (contentType) => {
    if (contentType === 'image/png') return '.png';
    if (contentType === 'image/webp') return '.webp';
    return '.jpg';
};

const validateImageForUpload = (image, contentType, fileName) => {
    if (!ALLOWED_IMAGE_TYPES.has(contentType)) {
        throw new Error(`Unsupported image type for ${fileName}. Allowed: JPG, PNG, WEBP.`);
    }

    if (image.fileSize && image.fileSize > MAX_IMAGE_SIZE_BYTES) {
        throw new Error(`${fileName} exceeds the 5 MB upload limit.`);
    }

    if (!image.uri) {
        throw new Error(`Invalid image selected for ${fileName}.`);
    }
};

const sanitizePublicUrls = (urls = []) =>
    urls
        .map((item) => {
            if (typeof item === 'string') {
                return item.trim();
            }

            if (item && typeof item === 'object') {
                return (
                    item.publicUrl ||
                    item.imageUrl ||
                    item.url ||
                    item.ImageUrl ||
                    item.ImageURL ||
                    ''
                ).trim();
            }

            return '';
        })
        .filter(Boolean);

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
            .addCase(updateParkingThunk.pending, (state) => {
                state.createLoading = true;
            })
            .addCase(updateParkingThunk.fulfilled, (state, action) => {
                state.createLoading = false;
                if (action.payload) {
                    const idx = state.myListings.findIndex((l) => l.id === action.payload.id);
                    if (idx !== -1) state.myListings[idx] = action.payload;
                }
            })
            .addCase(updateParkingThunk.rejected, (state) => {
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
