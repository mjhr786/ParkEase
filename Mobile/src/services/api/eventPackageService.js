import apiClient from './apiClient';
import { ENDPOINTS } from './endpoints';

class EventPackageService {
    getOnSale = async (params) => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.ON_SALE, { params });
        return response.data;
    };

    getVenuesOnSale = async () => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.VENUES_ON_SALE);
        return response.data;
    };

    getByVenueEvent = async (venueEventId) => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.BY_VENUE_EVENT(venueEventId));
        return response.data;
    };

    getByParking = async (parkingSpaceId) => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.BY_PARKING(parkingSpaceId));
        return response.data;
    };

    getPackageDetails = async (id) => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.BY_ID(id));
        return response.data;
    };

    getMyPackages = async () => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.MY);
        return response.data;
    };

    getMyAnalytics = async () => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.MY_ANALYTICS);
        return response.data;
    };

    getPackageAnalytics = async (id) => {
        const response = await apiClient.get(ENDPOINTS.EVENT_PACKAGES.ANALYTICS(id));
        return response.data;
    };

    createPackage = async (packageData) => {
        const response = await apiClient.post(ENDPOINTS.EVENT_PACKAGES.BASE, packageData);
        return response.data;
    };

    updatePackage = async (id, updateData) => {
        const response = await apiClient.put(ENDPOINTS.EVENT_PACKAGES.BY_ID(id), updateData);
        return response.data;
    };

    deactivatePackage = async (id) => {
        const response = await apiClient.post(ENDPOINTS.EVENT_PACKAGES.DEACTIVATE(id));
        return response.data;
    };

    purchasePackage = async (id, purchaseData) => {
        const response = await apiClient.post(ENDPOINTS.EVENT_PACKAGES.PURCHASE(id), purchaseData);
        return response.data;
    };
}

export const eventPackageService = new EventPackageService();
export default eventPackageService;
