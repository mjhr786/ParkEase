/**
 * NotificationService.js
 * Handles Firebase Cloud Messaging (FCM) for push notifications.
 * Includes permission requests, token generation, and message listeners.
 */

import { PermissionsAndroid, Platform } from 'react-native';
import { EventBus } from '../../utils/EventBus';
import messaging from '@react-native-firebase/messaging';

class NotificationService {
    async initialize() {
        if (this.isInitialized) {
            return;
        }

        // 1. Request permissions (required for iOS and Android 13+)
        const hasPermission = await this.requestUserPermission();
        if (!hasPermission) {
            console.log('[NotificationService] User declined notification permissions.');
            return;
        }

        await this.ensureRemoteMessagesRegistration();

        // 2. Get FCM Device Token
        const token = await this.getDeviceToken();
        if (token) {
            this.registerTokenWithBackend(token);
        }

        // 3. Listen for foreground messages
        this.unsubscribeForegroundListener = messaging().onMessage(async remoteMessage => {
            console.log('[NotificationService] Foreground message received:', remoteMessage);
            
            // Show a global banner instead of a disruptive alert
            EventBus.emit('SHOW_BANNER', {
                title: remoteMessage.notification?.title || 'New Notification',
                message: remoteMessage.notification?.body || 'You have a new message',
                type: 'info'
            });
        });

        // 4. Handle background/quit-state clicks
        messaging().onNotificationOpenedApp(remoteMessage => {
            console.log('[NotificationService] Notification caused app to open from background:', remoteMessage);
            this.handleNotificationOpening(remoteMessage);
        });

        messaging().getInitialNotification().then(remoteMessage => {
            if (remoteMessage) {
                console.log('[NotificationService] Notification caused app to open from quit state:', remoteMessage);
                this.handleNotificationOpening(remoteMessage);
            }
        });

        this.isInitialized = true;

        // 5. Setup background message handler (Outside the class typically, but we define it here)
        // See: messaging().setBackgroundMessageHandler(...) in index.js
    }

    async requestUserPermission() {
        if (Platform.OS === 'ios') {
            const authStatus = await messaging().requestPermission();
            const enabled =
                authStatus === messaging.AuthorizationStatus.AUTHORIZED ||
                authStatus === messaging.AuthorizationStatus.PROVISIONAL;
            return enabled;
        } else if (Platform.OS === 'android' && Platform.Version >= 33) {
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
        return true; // Android < 13 permissions are granted at install time
    }

    async getAuthorizedDeviceToken() {
        const hasPermission = await this.requestUserPermission();

        if (!hasPermission) {
            console.log('[NotificationService] Notification permission not granted. Skipping device token request.');
            return null;
        }

        return this.getDeviceToken();
    }

    async getDeviceToken() {
        try {
            await this.ensureRemoteMessagesRegistration();
            const token = await messaging().getToken();
            console.log('\n--- 🔥 FIREBASE DEVICE TOKEN ---');
            console.log(token);
            console.log('--------------------------------\n');
            return token;
        } catch (error) {
            console.error('[NotificationService] Failed to get device token:', error);
            return null;
        }
    }

    async ensureRemoteMessagesRegistration() {
        const isRegistered = messaging().isDeviceRegisteredForRemoteMessages;

        if (isRegistered) {
            return;
        }

        await messaging().registerDeviceForRemoteMessages();
    }

    registerTokenWithBackend(token) {
        // PLACEHOLDER: Send token to backend API
        console.log('[NotificationService] Registering token with assumed backend service...');
        // Example: await apiClient.post('/users/device-token', { token });
    }

    handleNotificationOpening(remoteMessage) {
        if (!remoteMessage) return;
        console.log('[NotificationService] Navigating based on notification data:', remoteMessage.data);
        // Logic for deep-linking based on remoteMessage.data
    }

    cleanup() {
        if (this.unsubscribeForegroundListener) {
            this.unsubscribeForegroundListener();
            this.unsubscribeForegroundListener = null;
        }
        this.isInitialized = false;
    }
}

export default new NotificationService();
