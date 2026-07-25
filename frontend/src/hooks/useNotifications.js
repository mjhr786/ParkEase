import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from '../config';
import { AUTH_CHANGED_EVENT } from '../utils/authEvents';

// Use empty string for production (same origin) or localhost for development
const API_URL = API_BASE_URL;

/**
 * Custom hook for SignalR real-time notifications.
 * Manages connection lifecycle, reconnection, and message handling.
 *
 * Auth changes: listens for `parkease:auth-changed` (same-tab) and `storage` (cross-tab).
 * Does not poll localStorage on a timer (previous 2s interval removed).
 */
export function useNotifications(onNotification) {
    const connectionRef = useRef(null);
    const onNotificationRef = useRef(onNotification);
    const [isConnected, setIsConnected] = useState(false);
    const [connectionError, setConnectionError] = useState(null);

    // Keep callback ref updated
    useEffect(() => {
        onNotificationRef.current = onNotification;
    }, [onNotification]);

    // Get token from localStorage
    const getAccessToken = useCallback(() => {
        return localStorage.getItem('accessToken');
    }, []);

    const connect = useCallback(async () => {
        // Don't create duplicate connections
        if (connectionRef.current) {
            return;
        }

        const token = getAccessToken();
        if (!token) {
            return;
        }

        // Build connection with JWT authentication
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_URL}/hubs/notifications`, {
                accessTokenFactory: () => localStorage.getItem('accessToken'),
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            // Quiet in production builds; keep Warning+ for real issues
            .configureLogging(
                import.meta.env.DEV ? signalR.LogLevel.Information : signalR.LogLevel.Warning
            )
            .build();

        // Must be > server KeepAlive (default 30s). Default client timeout is 30s and races the ping.
        connection.serverTimeoutInMilliseconds = 90000;
        connection.keepAliveIntervalInMilliseconds = 15000;

        // Handle notifications - use ref to avoid stale closure
        connection.on('ReceiveNotification', (notification) => {
            if (onNotificationRef.current) {
                onNotificationRef.current(notification);
            }
        });

        // Connection state handlers
        connection.onclose((error) => {
            connectionRef.current = null;
            setIsConnected(false);
            if (error) {
                setConnectionError(error.message);
            }
        });

        connection.onreconnecting(() => {
            setIsConnected(false);
        });

        connection.onreconnected(() => {
            setIsConnected(true);
            setConnectionError(null);
        });

        try {
            await connection.start();
            connectionRef.current = connection;
            setIsConnected(true);
            setConnectionError(null);
        } catch (err) {
            console.error('SignalR connection error:', err);
            setConnectionError(err.message);
            connectionRef.current = null;
        }
    }, [getAccessToken]);

    const disconnect = useCallback(async () => {
        if (connectionRef.current) {
            try {
                await connectionRef.current.stop();
            } catch (err) {
                console.error('Error disconnecting SignalR:', err);
            }
            connectionRef.current = null;
            setIsConnected(false);
        }
    }, []);

    // Connect on mount, disconnect on unmount (same as before)
    useEffect(() => {
        // Small delay to ensure token is available after login / page load
        const timer = setTimeout(() => {
            connect();
        }, 500);

        return () => {
            clearTimeout(timer);
            disconnect();
        };
    }, []); // Empty deps - only run on mount/unmount

    // React to auth without a 2s polling loop (functionality preserved via events)
    useEffect(() => {
        const syncConnectionToAuth = () => {
            const token = getAccessToken();
            if (token && !connectionRef.current) {
                connect();
            } else if (!token && connectionRef.current) {
                disconnect();
            }
        };

        // Same-tab: login / register / logout / token refresh / clearTokens
        const onAuthChanged = () => {
            syncConnectionToAuth();
        };

        // Cross-tab: another tab logged in/out
        const onStorage = (e) => {
            if (e.key === 'accessToken' || e.key === null) {
                syncConnectionToAuth();
            }
        };

        window.addEventListener(AUTH_CHANGED_EVENT, onAuthChanged);
        window.addEventListener('storage', onStorage);

        return () => {
            window.removeEventListener(AUTH_CHANGED_EVENT, onAuthChanged);
            window.removeEventListener('storage', onStorage);
        };
    }, [connect, disconnect, getAccessToken]);

    return { isConnected, connectionError, connect, disconnect };
}

export default useNotifications;
