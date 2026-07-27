/**
 * Pass Slice
 * State for user's parking passes
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getMyPassesThunk = createAsyncThunk(
    'pass/getMyPasses',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.PASSES.MY);
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const createPassThunk = createAsyncThunk(
    'pass/create',
    async (data, { rejectWithValue }) => {
        try {
            const response = await apiClient.post(ENDPOINTS.PASSES.BASE, data);
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    passes: [],
    loading: false,
    createLoading: false,
    error: null,
};

const passSlice = createSlice({
    name: 'pass',
    initialState,
    reducers: {
        clearPasses: () => initialState,
    },
    extraReducers: (builder) => {
        builder
            .addCase(getMyPassesThunk.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(getMyPassesThunk.fulfilled, (state, action) => {
                state.loading = false;
                state.passes = action.payload?.passes || action.payload?.items || action.payload || [];
            })
            .addCase(getMyPassesThunk.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload;
            })
            .addCase(createPassThunk.pending, (state) => {
                state.createLoading = true;
                state.error = null;
            })
            .addCase(createPassThunk.fulfilled, (state, action) => {
                state.createLoading = false;
                const createdPass = action.payload?.pass || action.payload;
                if (createdPass) {
                    state.passes = [createdPass, ...state.passes];
                }
            })
            .addCase(createPassThunk.rejected, (state, action) => {
                state.createLoading = false;
                state.error = action.payload;
            });
    },
});

export const { clearPasses } = passSlice.actions;
export default passSlice.reducer;
