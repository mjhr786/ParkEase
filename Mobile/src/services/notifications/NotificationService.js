/**
 * NotificationService.js
 * Handles Firebase Cloud Messaging (FCM) for push notifications.
 * Includes permission requests, token registration/deregistration,
 * foreground message handling, and notification-tap deep-link routing.
 */

import { PermissionsAndroid, Platform } from 'react-native';
import messaging from '@react-native-firebase/messaging';
import firebase from '@react-native-firebase/app';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { EventBus } from '../../utils/EventBus';
import apiClient from '../api/apiClient';
import ENDPOINTS from '../api/endpoints';
import logger from '../../utils/logger';
import Constants from 'expo-constants';
import { storageService } from '../storage/secureStorage';

const TAG = 'NotificationService';
const DEVICE_ID_STORAGE_KEY = 'parkease_device_id';

/**
 * Reads and persists a stable device identifier for backend upsert behavior.
 * Prefer Firebase Installations on Android to match backend expectations.
 */
async function getOrCreateDeviceId() {
    try {
        const stored = await AsyncStorage.getItem(DEVICE_ID_STORAGE_KEY);
        if (stored) {
            return stored;
        }

        const firebaseInstallationId = await getFirebaseInstallationId();
        const expoId = Constants?.installationId;
        const newId = firebaseInstallationId || expoId || generateUUID();

        await AsyncStorage.setItem(DEVICE_ID_STORAGE_KEY, newId);
        return newId;
    } catch (error) {
        logger.warn(TAG, 'Could not persist deviceId, using in-memory fallback', error);
        return generateUUID();
    }
}

async function getFirebaseInstallationId() {
    if (Platform.OS !== 'android') {
        return null;
    }

    try {
        return await firebase.installations().getId();
    } catch (error) {
        logger.warn(TAG, 'Failed to get Firebase installation ID, falling back to local identifier', error);
        return null;
    }
}

function generateUUID() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = (Math.random() * 16) | 0;
        const v = c === 'x' ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

/**
 * Maps an FCM notification type to a navigation action.
 * Returns { screen, params } or null if no navigation needed.
 */
function resolveNavigationTarget(data = {}) {
    const type = data.type || '';
    const bookingId = data.bookingId || data.BookingId;
    const parkingId = data.parkingSpaceId || data.ParkingSpaceId || data.parkingId;
    const conversationId = data.conversationId || data.ConversationId;

    const bookingTypes = [
        'booking.approved',
        'booking.rejected',
        'booking.cancelled',
        'booking.expiring',
        'booking.checkin',
        'booking.checkout',
        'payment.completed',
        'payment.failed',
    ];

    if (bookingTypes.includes(type) && bookingId) {
        return { screen: 'BookingDetail', params: { bookingId } };
    }

    if (parkingId) {
        return { screen: 'ParkingDetail', params: { parkingId } };
    }

    if (conversationId) {
        return { screen: 'ChatScreen', params: { conversationId } };
    }

    // Fallback: open Notifications screen for any unrecognised type
    if (type) {
        return { screen: 'Notifications', params: {} };
    }

    return null;
}

class NotificationService {
    constructor() {
        this.isInitialized = false;
        this._navigationRef = null;
        this._pendingNavigation = null; // queued while nav isn't ready yet
        this.unsubscribeForegroundListener = null;
        this.unsubscribeTokenRefresh = null;
        this.unsubscribeNotificationOpened = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Call from AppTabNavigator once the navigator is mounted and the user
     * is authenticated. Drains any queued navigation from quit-state taps.
     */
    setNavigationRef(navigationDispatch) {
        this._navigationRef = navigationDispatch;
        if (this._pendingNavigation) {
            this._navigateTo(this._pendingNavigation);
            this._pendingNavigation = null;
        }
    }

    /**
     * Initialize FCM: request permissions, get token, register with backend,
     * and set up message/open listeners. Safe to call multiple times.
     */
    async initialize() {
        if (Platform.OS !== 'android') {
            logger.info(TAG, 'Skipping notification initialization on non-Android platform');
            return;
        }

        if (this.isInitialized) {
            return;
        }

        // 1. Request permissions
        const hasPermission = await this.requestUserPermission();
        if (!hasPermission) {
            logger.warn(TAG, 'User declined notification permissions.');
            return;
        }

        await this._ensureRemoteMessagesRegistration();

        // 2. Listen for token refresh while the user is authenticated.
        this.unsubscribeTokenRefresh = messaging().onTokenRefresh(async (newToken) => {
            if (!(await this._hasAuthenticatedSession())) {
                logger.info(TAG, 'Skipping token refresh registration because no active session was found');
                return;
            }

            logger.info(TAG, 'FCM token refreshed — re-registering with backend');
            await this.registerTokenWithBackend(newToken);
        });

        // 3. Foreground messages → show a dismissible banner
        this.unsubscribeForegroundListener = messaging().onMessage(async (remoteMessage) => {
            logger.info(TAG, 'Foreground message received');
            EventBus.emit('SHOW_BANNER', {
                title: remoteMessage.notification?.title || 'New Notification',
                message: remoteMessage.notification?.body || 'You have a new message',
                type: 'info',
            });
        });

        // 4. Background tap: app was in background, user tapped the notification
        this.unsubscribeNotificationOpened = messaging().onNotificationOpenedApp((remoteMessage) => {
            logger.info(TAG, 'Notification opened app from background');
            this.handleNotificationOpening(remoteMessage);
        });

        // 5. Quit-state tap: app was closed, user tapped the notification
        messaging()
            .getInitialNotification()
            .then((remoteMessage) => {
                if (remoteMessage) {
                    logger.info(TAG, 'Notification opened app from quit state');
                    this.handleNotificationOpening(remoteMessage);
                }
            });

        this.isInitialized = true;
    }

    /**
     * Deregister the device token on logout.
     * Best-effort local cleanup only: backend currently has no deregister API.
     */
    async deregisterToken() {
        if (Platform.OS !== 'android') {
            logger.info(TAG, 'Skipping token deregistration on non-Android platform');
            this.cleanup();
            return;
        }

        try {
            await messaging().deleteToken();
            logger.info(TAG, 'Local FCM token deleted');
        } catch (error) {
            logger.warn(TAG, 'Failed to delete local FCM token (non-critical)', error);
        }

        // Clean up listeners and reset so initialize() runs fresh on next login
        this.cleanup();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    async requestUserPermission() {
        if (Platform.OS === 'android' && Platform.Version >= 33) {
            const hasPermission = await PermissionsAndroid.check(
                PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS
            );
            if (hasPermission) {
                return true;
            }
            const result = await PermissionsAndroid.request(
                PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS
            );
            return result === PermissionsAndroid.RESULTS.GRANTED;
        }

        return true; // Android < 13: granted at install time
    }

    async getDeviceToken() {
        if (Platform.OS !== 'android') {
            logger.info(TAG, 'Skipping device token lookup on non-Android platform');
            return null;
        }

        try {
            await this._ensureRemoteMessagesRegistration();
            const token = await messaging().getToken();
            logger.info(TAG, 'FCM token retrieved');
            return token;
        } catch (error) {
            logger.error(TAG, 'Failed to get device token', error);
            return null;
        }
    }

    /** Also exported for use outside (e.g. to check permission status). */
    async getAuthorizedDeviceToken() {
        if (Platform.OS !== 'android') {
            return null;
        }

        const hasPermission = await this.requestUserPermission();
        if (!hasPermission) {
            logger.warn(TAG, 'Permission not granted — skipping token request.');
            return null;
        }
        return this.getDeviceToken();
    }

    async registerTokenWithBackend(fcmToken) {
        if (Platform.OS !== 'android') {
            logger.info(TAG, 'Skipping backend token registration on non-Android platform');
            return false;
        }

        try {
            const deviceId = await getOrCreateDeviceId();
            const appVersion = Constants?.expoConfig?.version || Constants?.manifest?.version || null;

            const payload = {
                deviceId,
                platform: Platform.OS, // 'android' or 'ios'
                fcmToken,
                ...(appVersion ? { appVersion } : {}),
            };

            const response = await apiClient.post(ENDPOINTS.DEVICE_TOKENS.REGISTER, payload);

            if (response.data?.success) {
                logger.info(TAG, 'Device token registered successfully', { deviceId });
                return true;
            } else {
                logger.warn(TAG, 'Backend returned unexpected response for token registration', response.data);
            }
        } catch (error) {
            logger.error(TAG, 'Failed to register device token with backend', error);
        }

        return false;
    }

    async registerCurrentDevice() {
        if (Platform.OS !== 'android') {
            return false;
        }

        await this.initialize();

        const token = await this.getAuthorizedDeviceToken();
        if (!token) {
            return false;
        }

        return this.registerTokenWithBackend(token);
    }

    /**
     * Routes a tapped notification to the appropriate screen.
     * If the navigation ref isn't ready yet (quit state), queues the action.
     */
    handleNotificationOpening(remoteMessage) {
        if (!remoteMessage) {
            return;
        }

        const data = remoteMessage.data || {};
        const target = resolveNavigationTarget(data);

        if (!target) {
            logger.info(TAG, 'No navigation target for notification data', data);
            return;
        }

        if (this._navigationRef) {
            this._navigateTo(target);
        } else {
            // Navigator not mounted yet (quit-state tap) — queue it
            logger.info(TAG, 'Navigation not ready, queuing:', target);
            this._pendingNavigation = target;
        }
    }

    _navigateTo({ screen, params }) {
        try {
            this._navigationRef(screen, params);
        } catch (error) {
            logger.warn(TAG, 'Navigation failed', error);
        }
    }

    async _ensureRemoteMessagesRegistration() {
        if (Platform.OS !== 'android') {
            return;
        }

        const isRegistered = messaging().isDeviceRegisteredForRemoteMessages;
        if (!isRegistered) {
            await messaging().registerDeviceForRemoteMessages();
        }
    }

    async _hasAuthenticatedSession() {
        const accessToken = await storageService.getAccessToken();
        return Boolean(accessToken);
    }

    cleanup() {
        if (this.unsubscribeForegroundListener) {
            this.unsubscribeForegroundListener();
            this.unsubscribeForegroundListener = null;
        }
        if (this.unsubscribeTokenRefresh) {
            this.unsubscribeTokenRefresh();
            this.unsubscribeTokenRefresh = null;
        }
        if (this.unsubscribeNotificationOpened) {
            this.unsubscribeNotificationOpened();
            this.unsubscribeNotificationOpened = null;
        }
        this._navigationRef = null;
        this._pendingNavigation = null;
        this.isInitialized = false;
    }
}

export default new NotificationService();
