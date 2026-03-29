/**
 * Cache Service
 * AsyncStorage-based cache with TTL and stale-while-revalidate pattern
 */

import AsyncStorage from '@react-native-async-storage/async-storage';
import logger from '../../utils/logger';

const TAG = 'CacheService';
const CACHE_PREFIX = '@parkease_cache:';

// TTL in milliseconds
const TTL = {
    SHORT: 2 * 60 * 1000,      // 2 min — dashboard, stats
    MEDIUM: 5 * 60 * 1000,     // 5 min — listings, bookings
    LONG: 30 * 60 * 1000,      // 30 min — vehicles, favorites, profile
    PERMANENT: null,            // No expiry — onboarding flag, remember-me
};

const CACHE_KEYS = {
    DASHBOARD_MEMBER: 'dashboard_member',
    DASHBOARD_VENDOR: 'dashboard_vendor',
    MY_BOOKINGS: 'my_bookings',
    VENDOR_BOOKINGS: 'vendor_bookings',
    MY_LISTINGS: 'my_listings',
    VEHICLES: 'vehicles',
    FAVORITES: 'favorites',
    USER_PROFILE: 'user_profile',
    REMEMBER_EMAIL: 'remember_email',
    ONBOARDING_DONE: 'onboarding_done',
};

const cacheService = {
    /**
     * Set a cached value with optional TTL
     */
    async set(key, data, ttl = TTL.MEDIUM) {
        try {
            const entry = {
                data,
                timestamp: Date.now(),
                ttl,
            };
            await AsyncStorage.setItem(CACHE_PREFIX + key, JSON.stringify(entry));
        } catch (error) {
            logger.warn(TAG, `Failed to cache ${key}`, error);
        }
    },

    /**
     * Get a cached value. Returns null if expired or missing.
     */
    async get(key) {
        try {
            const raw = await AsyncStorage.getItem(CACHE_PREFIX + key);
            if (!raw) return null;

            const entry = JSON.parse(raw);
            // Check TTL (null = permanent)
            if (entry.ttl !== null && Date.now() - entry.timestamp > entry.ttl) {
                // Expired — clean up but still return stale data
                return { data: entry.data, stale: true };
            }
            return { data: entry.data, stale: false };
        } catch (error) {
            logger.warn(TAG, `Failed to read cache ${key}`, error);
            return null;
        }
    },

    /**
     * Get just the data (convenience). Returns null if missing.
     */
    async getData(key) {
        const result = await this.get(key);
        return result?.data ?? null;
    },

    /**
     * Invalidate a specific cache key
     */
    async invalidate(key) {
        try {
            await AsyncStorage.removeItem(CACHE_PREFIX + key);
        } catch (error) {
            logger.warn(TAG, `Failed to invalidate ${key}`, error);
        }
    },

    /**
     * Invalidate all cache
     */
    async invalidateAll() {
        try {
            const keys = await AsyncStorage.getAllKeys();
            const cacheKeys = keys.filter(k => k.startsWith(CACHE_PREFIX));
            if (cacheKeys.length > 0) {
                await AsyncStorage.multiRemove(cacheKeys);
            }
        } catch (error) {
            logger.warn(TAG, 'Failed to invalidate all cache', error);
        }
    },

    // Convenience setters with appropriate TTL
    async cacheDashboard(type, data) {
        await this.set(
            type === 'member' ? CACHE_KEYS.DASHBOARD_MEMBER : CACHE_KEYS.DASHBOARD_VENDOR,
            data,
            TTL.SHORT
        );
    },

    async cacheBookings(type, data) {
        await this.set(
            type === 'my' ? CACHE_KEYS.MY_BOOKINGS : CACHE_KEYS.VENDOR_BOOKINGS,
            data,
            TTL.MEDIUM
        );
    },

    async cacheVehicles(data) {
        await this.set(CACHE_KEYS.VEHICLES, data, TTL.LONG);
    },

    async cacheFavorites(data) {
        await this.set(CACHE_KEYS.FAVORITES, data, TTL.LONG);
    },

    async cacheUserProfile(data) {
        await this.set(CACHE_KEYS.USER_PROFILE, data, TTL.LONG);
    },

    // Remember email
    async setRememberEmail(email) {
        await this.set(CACHE_KEYS.REMEMBER_EMAIL, email, TTL.PERMANENT);
    },

    async getRememberEmail() {
        return this.getData(CACHE_KEYS.REMEMBER_EMAIL);
    },

    async clearRememberEmail() {
        await this.invalidate(CACHE_KEYS.REMEMBER_EMAIL);
    },

    // Onboarding
    async setOnboardingDone() {
        await this.set(CACHE_KEYS.ONBOARDING_DONE, true, TTL.PERMANENT);
    },

    async isOnboardingDone() {
        return (await this.getData(CACHE_KEYS.ONBOARDING_DONE)) === true;
    },
};

export { TTL, CACHE_KEYS };
export default cacheService;
