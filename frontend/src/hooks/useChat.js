import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from '../config';

const API_URL = API_BASE_URL;

/**
 * Custom hook for SignalR real-time chat.
 * Manages connection lifecycle, reconnection, and message handling.
 */
export function useChat(onMessageReceived, onMessagesRead) {
    const connectionRef = useRef(null);
    const onMessageRef = useRef(onMessageReceived);
    const onReadRef = useRef(onMessagesRead);
    const [isConnected, setIsConnected] = useState(false);

    useEffect(() => {
        onMessageRef.current = onMessageReceived;
    }, [onMessageReceived]);

    useEffect(() => {
        onReadRef.current = onMessagesRead;
    }, [onMessagesRead]);

    const getAccessToken = useCallback(() => {
        return localStorage.getItem('accessToken');
    }, []);

    const connect = useCallback(async () => {
        if (connectionRef.current) return;

        const token = getAccessToken();
        if (!token) return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_URL}/hubs/chat`, {
                accessTokenFactory: () => token,
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on('ReceiveMessage', (message) => {
            if (onMessageRef.current) onMessageRef.current(message);
        });

        connection.on('MessagesRead', (conversationId) => {
            if (onReadRef.current) onReadRef.current(conversationId);
        });

        connection.on('Error', (error) => {
            console.error('Chat hub error:', error);
        });

        connection.onclose(() => {
            connectionRef.current = null;
            setIsConnected(false);
        });

        connection.onreconnected(() => setIsConnected(true));
        connection.onreconnecting(() => setIsConnected(false));

        try {
            await connection.start();
            connectionRef.current = connection;
            setIsConnected(true);
        } catch (err) {
            console.error('Chat SignalR connection error:', err);
            connectionRef.current = null;
        }
    }, [getAccessToken]);

    const disconnect = useCallback(async () => {
        if (connectionRef.current) {
            try { await connectionRef.current.stop(); } catch { }
            connectionRef.current = null;
            setIsConnected(false);
        }
    }, []);

    useEffect(() => {
        const timer = setTimeout(() => connect(), 300);
        return () => {
            clearTimeout(timer);
            disconnect();
        };
    }, []);

    return { isConnected, connect, disconnect };
}

export default useChat;
