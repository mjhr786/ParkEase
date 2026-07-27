import apiClient from './apiClient';
import { ENDPOINTS } from './endpoints';

class IotService {
    // LPR Settings
    getCameraKeys = async (parkingSpaceId) => {
        const response = await apiClient.get(ENDPOINTS.LPR.CAMERA_KEYS(parkingSpaceId));
        return response.data;
    };

    createCameraKey = async (parkingSpaceId, keyData) => {
        const response = await apiClient.post(ENDPOINTS.LPR.CAMERA_KEYS(parkingSpaceId), keyData);
        return response.data;
    };

    toggleCameraKey = async (parkingSpaceId, keyId, isEnabled) => {
        const response = await apiClient.put(ENDPOINTS.LPR.CAMERA_KEY_TOGGLE(parkingSpaceId, keyId), { isEnabled });
        return response.data;
    };

    deleteCameraKey = async (parkingSpaceId, keyId) => {
        const response = await apiClient.delete(ENDPOINTS.LPR.CAMERA_KEY_DELETE(parkingSpaceId, keyId));
        return response.data;
    };

    getPlateRules = async (parkingSpaceId) => {
        const response = await apiClient.get(ENDPOINTS.LPR.PLATE_RULES(parkingSpaceId));
        return response.data;
    };

    createPlateRule = async (parkingSpaceId, ruleData) => {
        const response = await apiClient.post(ENDPOINTS.LPR.PLATE_RULES(parkingSpaceId), ruleData);
        return response.data;
    };

    togglePlateRule = async (parkingSpaceId, ruleId, isEnabled) => {
        const response = await apiClient.put(ENDPOINTS.LPR.PLATE_RULE_TOGGLE(parkingSpaceId, ruleId), { isEnabled });
        return response.data;
    };

    deletePlateRule = async (parkingSpaceId, ruleId) => {
        const response = await apiClient.delete(ENDPOINTS.LPR.PLATE_RULE_DELETE(parkingSpaceId, ruleId));
        return response.data;
    };

    // EV / IoT Simulator
    simulateEvSession = async (simulationData) => {
        const response = await apiClient.post(ENDPOINTS.IOT.OCPP_SIMULATE, simulationData);
        return response.data;
    };

    simulateLprEvent = async (simulationData) => {
        const response = await apiClient.post(ENDPOINTS.IOT.LPR_SIMULATE, simulationData);
        return response.data;
    };
}

export const iotService = new IotService();
export default iotService;
