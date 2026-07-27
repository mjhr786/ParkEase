import apiClient from './apiClient';
import { ENDPOINTS } from './endpoints';
import logger from '../../utils/logger';

const TAG = 'VehicleService';

export const vehicleService = {
    /**
     * Get all vehicles for the current user
     */
    getVehicles: async () => {
        try {
            const response = await apiClient.get(ENDPOINTS.VEHICLES.BASE);
            return { success: true, data: response.data };
        } catch (error) {
            logger.error(TAG, 'Failed to fetch vehicles', error);
            throw error;
        }
    },

    /**
     * Add a new vehicle
     */
    addVehicle: async (data) => {
        try {
            const response = await apiClient.post(ENDPOINTS.VEHICLES.BASE, data);
            return { success: true, data: response.data };
        } catch (error) {
            logger.error(TAG, 'Failed to add vehicle', error);
            throw error;
        }
    },

    /**
     * Update an existing vehicle
     */
    updateVehicle: async (id, data) => {
        try {
            const response = await apiClient.put(`${ENDPOINTS.VEHICLES.BASE}/${id}`, data);
            return { success: true, data: response.data };
        } catch (error) {
            logger.error(TAG, `Failed to update vehicle ${id}`, error);
            throw error;
        }
    },

    /**
     * Delete a vehicle
     */
    deleteVehicle: async (id) => {
        try {
            const response = await apiClient.delete(`${ENDPOINTS.VEHICLES.BASE}/${id}`);
            return { success: true, data: response.data };
        } catch (error) {
            logger.error(TAG, `Failed to delete vehicle ${id}`, error);
            throw error;
        }
    }
};

export default vehicleService;
