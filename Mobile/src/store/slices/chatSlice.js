/**
 * Chat Slice
 * State for conversations, messages, and unread count
 */

import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import apiClient from '../../services/api/apiClient';
import ENDPOINTS from '../../services/api/endpoints';
import { getErrorMessage } from '../../utils/errorHandler';

export const getConversationsThunk = createAsyncThunk(
    'chat/getConversations',
    async (params = {}, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.CHAT.CONVERSATIONS, { params });
            return response.data.data || response.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getMessagesThunk = createAsyncThunk(
    'chat/getMessages',
    async ({ conversationId, page = 1, pageSize = 50 }, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.CHAT.MESSAGES(conversationId), {
                params: { page, pageSize },
            });
            return { conversationId, ...(response.data.data || response.data) };
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const sendMessageThunk = createAsyncThunk(
    'chat/sendMessage',
    async ({ parkingSpaceId, content, conversationId }, { rejectWithValue }) => {
        try {
            const payload = { parkingSpaceId, content };
            if (conversationId) {
                payload.conversationId = conversationId;
            }
            const response = await apiClient.post(ENDPOINTS.CHAT.SEND, payload);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

export const getUnreadCountThunk = createAsyncThunk(
    'chat/getUnreadCount',
    async (_, { rejectWithValue }) => {
        try {
            const response = await apiClient.get(ENDPOINTS.CHAT.UNREAD_COUNT);
            return response.data.data;
        } catch (error) {
            return rejectWithValue(getErrorMessage(error));
        }
    }
);

const initialState = {
    conversations: [],
    conversationsLoading: false,
    messages: {},         // keyed by conversationId
    messagesLoading: false,
    sendLoading: false,
    unreadCount: 0,
};

const chatSlice = createSlice({
    name: 'chat',
    initialState,
    reducers: {
        clearMessages: (state, action) => {
            if (action.payload) {
                delete state.messages[action.payload];
            } else {
                state.messages = {};
            }
        },
        addLocalMessage: (state, action) => {
            const { conversationId, message } = action.payload;
            if (!state.messages[conversationId]) {
                state.messages[conversationId] = [];
            }
            state.messages[conversationId].push(message);
        },
    },
    extraReducers: (builder) => {
        builder
            .addCase(getConversationsThunk.pending, (state) => {
                state.conversationsLoading = true;
            })
            .addCase(getConversationsThunk.fulfilled, (state, action) => {
                state.conversationsLoading = false;
                state.conversations = action.payload?.conversations || action.payload || [];
            })
            .addCase(getConversationsThunk.rejected, (state) => {
                state.conversationsLoading = false;
            })
            .addCase(getMessagesThunk.pending, (state) => {
                state.messagesLoading = true;
            })
            .addCase(getMessagesThunk.fulfilled, (state, action) => {
                state.messagesLoading = false;
                const { conversationId, messages } = action.payload;
                state.messages[conversationId] = messages || [];
            })
            .addCase(getMessagesThunk.rejected, (state) => {
                state.messagesLoading = false;
            })
            .addCase(sendMessageThunk.pending, (state) => {
                state.sendLoading = true;
            })
            .addCase(sendMessageThunk.fulfilled, (state, action) => {
                state.sendLoading = false;
                if (action.payload?.conversation) {
                    const idx = state.conversations.findIndex(
                        (c) => c.id === action.payload.conversation.id
                    );
                    if (idx !== -1) {
                        state.conversations[idx] = action.payload.conversation;
                    } else {
                        state.conversations.unshift(action.payload.conversation);
                    }
                }
            })
            .addCase(sendMessageThunk.rejected, (state) => {
                state.sendLoading = false;
            })
            .addCase(getUnreadCountThunk.fulfilled, (state, action) => {
                state.unreadCount = action.payload?.unreadCount ?? action.payload ?? 0;
            });
    },
});

export const { clearMessages, addLocalMessage } = chatSlice.actions;
export default chatSlice.reducer;
