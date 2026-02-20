/**
 * Auth Service
 * Authentication API wrappers
 */

import apiClient from '../api/apiClient';
import ENDPOINTS from '../api/endpoints';
import { storageService } from '../storage/secureStorage';
import logger from '../../utils/logger';

const TAG = 'AuthService';

export const authService = {
    async login(credentials) {
        logger.info(TAG, 'Login attempt', { email: credentials.email });
        const response = await apiClient.post(ENDPOINTS.AUTH.LOGIN, credentials);
        if (response.data.success && response.data.data) {
            const { accessToken, refreshToken, user } = response.data.data;
            await storageService.setTokens(accessToken, refreshToken);
            await storageService.setUser(user);
        }
        return response.data;
    },

    async register(data) {
        logger.info(TAG, 'Register attempt', { email: data.email, role: data.role });
        const response = await apiClient.post(ENDPOINTS.AUTH.REGISTER, data);
        if (response.data.success && response.data.data) {
            const { accessToken, refreshToken, user } = response.data.data;
            await storageService.setTokens(accessToken, refreshToken);
            await storageService.setUser(user);
        }
        return response.data;
    },

    async logout() {
        try {
            await apiClient.post(ENDPOINTS.AUTH.LOGOUT);
        } catch (error) {
            logger.warn(TAG, 'Logout API call failed', error);
        }
        await storageService.clearAll();
    },

    async refreshToken() {
        const refreshToken = await storageService.getRefreshToken();
        if (!refreshToken) return null;

        const response = await apiClient.post(ENDPOINTS.AUTH.REFRESH, { refreshToken });
        if (response.data.success && response.data.data) {
            const { accessToken, refreshToken: newRefreshToken } = response.data.data;
            await storageService.setTokens(accessToken, newRefreshToken);
            return response.data.data;
        }
        return null;
    },

    async changePassword(data) {
        const response = await apiClient.post(ENDPOINTS.AUTH.CHANGE_PASSWORD, data);
        return response.data;
    },

    async getCurrentUser() {
        const response = await apiClient.get(ENDPOINTS.USERS.ME);
        return response.data;
    },

    async updateProfile(data) {
        const response = await apiClient.put(ENDPOINTS.USERS.ME, data);
        return response.data;
    },

    async deleteAccount() {
        const response = await apiClient.delete(ENDPOINTS.USERS.ME);
        await storageService.clearAll();
        return response.data;
    },

    /** Try restoring session from stored tokens */
    async tryRestoreSession() {
        const token = await storageService.getAccessToken();
        if (!token) return null;

        try {
            const response = await apiClient.get(ENDPOINTS.USERS.ME);
            if (response.data.success && response.data.data) {
                return response.data.data;
            }
        } catch (error) {
            logger.warn(TAG, 'Session restore failed', error);
            await storageService.clearAll();
        }
        return null;
    },
};

export default authService;
