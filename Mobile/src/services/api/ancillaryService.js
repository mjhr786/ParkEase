import apiClient from './apiClient';
import { ENDPOINTS } from './endpoints';

class AncillaryService {
    getByParking = async (parkingSpaceId) => {
        const response = await apiClient.get(ENDPOINTS.ANCILLARY_SERVICES.BY_PARKING(parkingSpaceId));
        return response.data;
    };

    getMyServices = async () => {
        const response = await apiClient.get(ENDPOINTS.ANCILLARY_SERVICES.MY);
        return response.data;
    };

    createService = async (serviceData) => {
        const response = await apiClient.post(ENDPOINTS.ANCILLARY_SERVICES.BASE, serviceData);
        return response.data;
    };

    updateService = async (id, updateData) => {
        const response = await apiClient.put(ENDPOINTS.ANCILLARY_SERVICES.BY_ID(id), updateData);
        return response.data;
    };

    deactivateService = async (id) => {
        const response = await apiClient.post(ENDPOINTS.ANCILLARY_SERVICES.DEACTIVATE(id));
        return response.data;
    };
}

export const ancillaryService = new AncillaryService();
export default ancillaryService;
