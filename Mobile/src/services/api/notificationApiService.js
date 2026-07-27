import apiClient from './apiClient';
import { ENDPOINTS } from './endpoints';
import logger from '../../utils/logger';

const TAG = 'NotificationApiService';

export const notificationApiService = {
    /**
     * Get all notifications for the current user
     */
    getNotifications: async () => {
        try {
            const response = await apiClient.get(ENDPOINTS.NOTIFICATIONS.BASE);
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to fetch notifications', error);
            throw error;
        }
    },

    /**
     * Mark a specific notification as read
     */
    markAsRead: async (id) => {
        try {
            const response = await apiClient.post(ENDPOINTS.NOTIFICATIONS.MARK_READ(id));
            return response.data;
        } catch (error) {
            logger.error(TAG, `Failed to mark notification ${id} as read`, error);
            throw error;
        }
    },

    /**
     * Mark all notifications as read
     */
    markAllAsRead: async () => {
        try {
            const response = await apiClient.post(ENDPOINTS.NOTIFICATIONS.MARK_ALL_READ);
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to mark all notifications as read', error);
            throw error;
        }
    },

    /**
     * Delete a notification
     */
    deleteNotification: async (id) => {
        try {
            const response = await apiClient.delete(ENDPOINTS.NOTIFICATIONS.DELETE(id));
            return response.data;
        } catch (error) {
            logger.error(TAG, `Failed to delete notification ${id}`, error);
            throw error;
        }
    },

    /**
     * Clear all notifications
     */
    clearAll: async () => {
        try {
            const response = await apiClient.delete(ENDPOINTS.NOTIFICATIONS.CLEAR_ALL);
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to clear all notifications', error);
            throw error;
        }
    }
};

export default notificationApiService;
