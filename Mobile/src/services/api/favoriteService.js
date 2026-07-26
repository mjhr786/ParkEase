import apiClient from './apiClient';
import { ENDPOINTS } from './endpoints';
import logger from '../../utils/logger';

const TAG = 'FavoriteService';

export const favoriteService = {
    /**
     * Get all favorite parking spaces for the current user
     */
    getFavorites: async () => {
        try {
            const response = await apiClient.get(ENDPOINTS.FAVORITES.BASE);
            return response.data;
        } catch (error) {
            logger.error(TAG, 'Failed to fetch favorites', error);
            throw error;
        }
    },

    /**
     * Toggle favorite status of a parking space
     */
    toggleFavorite: async (parkingSpaceId) => {
        try {
            const response = await apiClient.post(ENDPOINTS.FAVORITES.TOGGLE(parkingSpaceId));
            return response.data;
        } catch (error) {
            logger.error(TAG, `Failed to toggle favorite for space ${parkingSpaceId}`, error);
            throw error;
        }
    }
};

export default favoriteService;
