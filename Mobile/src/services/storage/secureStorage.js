/**
 * Secure Storage Service
 * Uses expo-secure-store for sensitive data and AsyncStorage for non-sensitive
 */

import * as SecureStore from 'expo-secure-store';
import AsyncStorage from '@react-native-async-storage/async-storage';

const KEYS = {
    ACCESS_TOKEN: 'parkease_access_token',
    REFRESH_TOKEN: 'parkease_refresh_token',
    USER: 'parkease_user',
};

export const storageService = {
    // Secure storage for tokens
    async setAccessToken(token) {
        await SecureStore.setItemAsync(KEYS.ACCESS_TOKEN, token);
    },

    async getAccessToken() {
        return SecureStore.getItemAsync(KEYS.ACCESS_TOKEN);
    },

    async setRefreshToken(token) {
        await SecureStore.setItemAsync(KEYS.REFRESH_TOKEN, token);
    },

    async getRefreshToken() {
        return SecureStore.getItemAsync(KEYS.REFRESH_TOKEN);
    },

    async setTokens(accessToken, refreshToken) {
        await Promise.all([
            SecureStore.setItemAsync(KEYS.ACCESS_TOKEN, accessToken),
            SecureStore.setItemAsync(KEYS.REFRESH_TOKEN, refreshToken),
        ]);
    },

    async clearTokens() {
        await Promise.all([
            SecureStore.deleteItemAsync(KEYS.ACCESS_TOKEN),
            SecureStore.deleteItemAsync(KEYS.REFRESH_TOKEN),
        ]);
    },

    // Regular storage for user data
    async setUser(user) {
        await AsyncStorage.setItem(KEYS.USER, JSON.stringify(user));
    },

    async getUser() {
        const data = await AsyncStorage.getItem(KEYS.USER);
        return data ? JSON.parse(data) : null;
    },

    async clearUser() {
        await AsyncStorage.removeItem(KEYS.USER);
    },

    // Clear all
    async clearAll() {
        await Promise.all([
            SecureStore.deleteItemAsync(KEYS.ACCESS_TOKEN),
            SecureStore.deleteItemAsync(KEYS.REFRESH_TOKEN),
            AsyncStorage.removeItem(KEYS.USER),
        ]);
    },

    // Generic async storage
    async setItem(key, value) {
        await AsyncStorage.setItem(key, JSON.stringify(value));
    },

    async getItem(key) {
        const item = await AsyncStorage.getItem(key);
        return item ? JSON.parse(item) : null;
    },

    async removeItem(key) {
        await AsyncStorage.removeItem(key);
    },
};

export default storageService;
