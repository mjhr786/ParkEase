/**
 * Chat API Service
 * Handles all chat-related API calls
 */

import apiClient from '../api/apiClient';
import logger from '../../utils/logger';

const TAG = 'ChatService';

const chatService = {
    /**
     * Get user's conversations (paginated)
     */
    async getConversations(page = 1, pageSize = 20) {
        try {
            const response = await apiClient.get(`/chat/conversations?page=${page}&pageSize=${pageSize}`);
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to get conversations', error);
            throw error;
        }
    },

    /**
     * Get messages for a conversation (paginated, newest first)
     */
    async getMessages(conversationId, page = 1, pageSize = 50) {
        try {
            const response = await apiClient.get(`/chat/conversations/${conversationId}/messages?page=${page}&pageSize=${pageSize}`);
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to get messages', error);
            throw error;
        }
    },

    /**
     * Send a message (creates conversation if needed)
     */
    async sendMessage(parkingSpaceId, content) {
        try {
            const response = await apiClient.post('/chat/send', { parkingSpaceId, content });
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to send message', error);
            throw error;
        }
    },

    /**
     * Mark all messages in a conversation as read
     */
    async markAsRead(conversationId) {
        try {
            const response = await apiClient.post(`/chat/conversations/${conversationId}/read`);
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to mark messages as read', error);
            throw error;
        }
    },
};

export default chatService;
