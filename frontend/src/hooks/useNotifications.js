import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5129';

/**
 * Custom hook for SignalR real-time notifications.
 * Manages connection lifecycle, reconnection, and message handling.
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
            console.log('SignalR connection already exists');
            return;
        }

        const token = getAccessToken();
        if (!token) {
            console.log('No token available, skipping SignalR connection');
            return;
        }

        console.log('Connecting to SignalR hub at:', `${API_URL}/hubs/notifications`);

        // Build connection with JWT authentication
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_URL}/hubs/notifications`, {
                accessTokenFactory: () => token,
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Handle notifications - use ref to avoid stale closure
        connection.on('ReceiveNotification', (notification) => {
            console.log('📬 Notification received:', notification);
            if (onNotificationRef.current) {
                onNotificationRef.current(notification);
            }
        });

        // Connection state handlers
        connection.onclose((error) => {
            console.log('SignalR disconnected', error);
            connectionRef.current = null;
            setIsConnected(false);
            if (error) {
                setConnectionError(error.message);
            }
        });

        connection.onreconnecting((error) => {
            console.log('SignalR reconnecting...', error);
            setIsConnected(false);
        });

        connection.onreconnected((connectionId) => {
            console.log('SignalR reconnected', connectionId);
            setIsConnected(true);
            setConnectionError(null);
        });

        try {
            await connection.start();
            console.log('✅ SignalR connected successfully');
            connectionRef.current = connection;
            setIsConnected(true);
            setConnectionError(null);
        } catch (err) {
            console.error('❌ SignalR connection error:', err);
            setConnectionError(err.message);
            connectionRef.current = null;
        }
    }, [getAccessToken]);

    const disconnect = useCallback(async () => {
        if (connectionRef.current) {
            try {
                await connectionRef.current.stop();
                console.log('SignalR disconnected');
            } catch (err) {
                console.error('Error disconnecting SignalR:', err);
            }
            connectionRef.current = null;
            setIsConnected(false);
        }
    }, []);

    // Connect on mount, disconnect on unmount
    useEffect(() => {
        // Small delay to ensure token is available after login
        const timer = setTimeout(() => {
            connect();
        }, 500);

        return () => {
            clearTimeout(timer);
            disconnect();
        };
    }, []); // Empty deps - only run on mount/unmount

    // Listen for auth changes
    useEffect(() => {
        const handleAuthChange = () => {
            const token = getAccessToken();
            if (token && !connectionRef.current) {
                connect();
            } else if (!token && connectionRef.current) {
                disconnect();
            }
        };

        // Check periodically for token changes (backup for same-tab login)
        const interval = setInterval(handleAuthChange, 2000);

        return () => clearInterval(interval);
    }, [connect, disconnect, getAccessToken]);

    return { isConnected, connectionError, connect, disconnect };
}

export default useNotifications;
