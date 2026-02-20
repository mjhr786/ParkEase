/**
 * API Client
 * Axios instance with auth interceptors and token refresh
 */

import axios from 'axios';
import environment from '../../config/environment';
import { storageService } from '../storage/secureStorage';
import logger from '../../utils/logger';

const TAG = 'ApiClient';

// Create axios instance
const apiClient = axios.create({
    baseURL: environment.apiUrl,
    timeout: 30000,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Track refresh state to prevent concurrent refreshes
let isRefreshing = false;
let refreshSubscribers = [];

const subscribeTokenRefresh = (callback) => {
    refreshSubscribers.push(callback);
};

const onTokenRefreshed = (newToken) => {
    refreshSubscribers.forEach((callback) => callback(newToken));
    refreshSubscribers = [];
};

// Request interceptor - attach token
apiClient.interceptors.request.use(
    async (config) => {
        try {
            const token = await storageService.getAccessToken();
            if (token) {
                config.headers.Authorization = `Bearer ${token}`;
            }
        } catch (error) {
            logger.error(TAG, 'Failed to get access token', error);
        }
        return config;
    },
    (error) => Promise.reject(error)
);

// Response interceptor - handle 401 & token refresh
apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;

        // Skip refresh for auth endpoints
        const isAuthEndpoint = originalRequest.url?.includes('/auth/');

        if (error.response?.status === 401 && !originalRequest._retry && !isAuthEndpoint) {
            if (isRefreshing) {
                // Wait for ongoing refresh
                return new Promise((resolve) => {
                    subscribeTokenRefresh((newToken) => {
                        originalRequest.headers.Authorization = `Bearer ${newToken}`;
                        resolve(apiClient(originalRequest));
                    });
                });
            }

            originalRequest._retry = true;
            isRefreshing = true;

            try {
                const refreshToken = await storageService.getRefreshToken();
                if (!refreshToken) {
                    throw new Error('No refresh token');
                }

                const response = await axios.post(
                    `${environment.apiUrl}/auth/refresh`,
                    { refreshToken },
                    { headers: { 'Content-Type': 'application/json' } }
                );

                if (response.data.success && response.data.data) {
                    const { accessToken, refreshToken: newRefreshToken } = response.data.data;
                    await storageService.setTokens(accessToken, newRefreshToken);
                    originalRequest.headers.Authorization = `Bearer ${accessToken}`;
                    onTokenRefreshed(accessToken);
                    return apiClient(originalRequest);
                }

                throw new Error('Token refresh failed');
            } catch (refreshError) {
                logger.error(TAG, 'Token refresh failed', refreshError);
                await storageService.clearAll();
                // The auth state will be reset by the store listener
                return Promise.reject(refreshError);
            } finally {
                isRefreshing = false;
            }
        }

        return Promise.reject(error);
    }
);

export default apiClient;
