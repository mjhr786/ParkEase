/**
 * API Client
 * Axios instance with auth interceptors and token refresh
 */

import axios from 'axios';
import environment from '../../config/environment';
import { storageService } from '../storage/secureStorage';
import logger from '../../utils/logger';
import { EventBus } from '../../utils/EventBus';

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
        // Fix for Axios overriding the '/api' path:
        // Ensure the relative URL does not start with a slash 
        // and the baseURL ends with a slash so it resolves correctly
        if (config.url && config.url.startsWith('/')) {
            config.url = config.url.substring(1);
        }
        if (!config.baseURL.endsWith('/')) {
            config.baseURL += '/';
        }

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
        // Log silently using logger to avoid triggering the visual React Native LogBox popups on screen
        logger.debug(
            TAG,
            `[API Error] ${error.config?.url} - ${error.response?.status}`,
            error.response?.data || error.message
        );

        const originalRequest = error.config;

        // Skip refresh for auth endpoints
        const isAuthEndpoint = originalRequest?.url?.includes('/auth/');

        // Emit global error banner for network/server failures or unhandled server crashes
        // We avoid showing generic banners for 401 (token refresh handles it silently), 
        // 404 (handled by ui state), or 422 (validation errors handled by form)
        if (!error.response || (error.response.status !== 401 && error.response.status !== 404 && error.response.status !== 422)) {
            EventBus.emit('SHOW_BANNER', {
                title: 'Network Issue',
                message: 'Failed to connect to server. Please check your internet.'
            });
        } else if (error.code === 'ECONNABORTED') {
            EventBus.emit('SHOW_BANNER', {
                title: 'Timeout',
                message: 'Request took too long. Please try again.'
            });
        } else if (error.response && error.response.status === 401 && isAuthEndpoint) {
            // If it's a 401 on login/auth, show invalid credentials
            EventBus.emit('SHOW_BANNER', {
                title: 'Authentication Failed',
                message: error.response?.data?.message || 'Invalid email or password.'
            });
        }

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
