import { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from '../config';
import { useAuth } from './AuthContext';
import toast from 'react-hot-toast';
import api from '../services/api';

const ChatContext = createContext(null);

export function ChatProvider({ children }) {
    const { isAuthenticated, user } = useAuth();
    const [unreadCount, setUnreadCount] = useState(0);
    const [isConnected, setIsConnected] = useState(false);
    const connectionRef = useRef(null);
    const onMessageCallbackRef = useRef(null);
    const onReadCallbackRef = useRef(null);
    const activeConversationRef = useRef(null);

    // Provide a way for Chat page to set the active conversation
    const setActiveConversation = useCallback((id) => {
        activeConversationRef.current = id;
    }, []);

    // Public methods to let Chat page register its callbacks
    const registerMessageCallback = useCallback((cb) => {
        onMessageCallbackRef.current = cb;
    }, []);

    const unregisterMessageCallback = useCallback(() => {
        onMessageCallbackRef.current = null;
    }, []);

    const registerReadCallback = useCallback((cb) => {
        onReadCallbackRef.current = cb;
    }, []);

    const unregisterReadCallback = useCallback(() => {
        onReadCallbackRef.current = null;
    }, []);

    // Fetch initial unread count
    const refreshUnreadCount = useCallback(async () => {
        if (!isAuthenticated) return;
        try {
            const result = await api.getUnreadCount();
            if (result.success) {
                setUnreadCount(result.data || 0);
            }
        } catch {
            // Silently fail
        }
    }, [isAuthenticated]);

    // SignalR connection
    useEffect(() => {
        if (!isAuthenticated) {
            // Disconnect if not authenticated
            if (connectionRef.current) {
                connectionRef.current.stop().catch(() => { });
                connectionRef.current = null;
                setIsConnected(false);
            }
            setUnreadCount(0);
            return;
        }

        // Fetch initial unread count
        refreshUnreadCount();

        const token = localStorage.getItem('accessToken');
        if (!token) return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_BASE_URL}/hubs/chat`, {
                accessTokenFactory: () => token,
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets,
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on('ReceiveMessage', (message) => {
            // Forward to Chat page callback if registered
            if (onMessageCallbackRef.current) {
                onMessageCallbackRef.current(message);
            }

            // Show toast & increment unread if message is from someone else AND not in the currently active conversation
            if (message.senderId !== user?.id && message.conversationId !== activeConversationRef.current) {
                setUnreadCount(prev => prev + 1);

                // Show toast notification
                toast((t) => (
                    <div
                        onClick={() => {
                            toast.dismiss(t.id);
                            window.location.href = `/chat/${message.conversationId}`;
                        }}
                        style={{ cursor: 'pointer' }}
                    >
                        <div style={{ fontWeight: '600', marginBottom: '4px' }}>
                            💬 {message.senderName}
                        </div>
                        <div style={{ fontSize: '0.85em', opacity: 0.9 }}>
                            {message.content?.length > 60
                                ? message.content.substring(0, 60) + '...'
                                : message.content}
                        </div>
                    </div>
                ), {
                    duration: 5000,
                    icon: null,
                });
            } else if (message.conversationId === activeConversationRef.current) {
                // If they are currently looking at the conversation, mark it as read immediately
                api.markAsRead(message.conversationId).catch(() => { });
            }
        });

        connection.on('MessagesRead', (conversationId) => {
            if (onReadCallbackRef.current) {
                onReadCallbackRef.current(conversationId);
            }
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

        const timer = setTimeout(async () => {
            try {
                await connection.start();
                connectionRef.current = connection;
                setIsConnected(true);
            } catch (err) {
                console.error('Chat SignalR connection error:', err);
            }
        }, 500);

        return () => {
            clearTimeout(timer);
            if (connectionRef.current) {
                connectionRef.current.stop().catch(() => { });
                connectionRef.current = null;
                setIsConnected(false);
            }
        };
    }, [isAuthenticated, user?.id]);

    const decrementUnread = useCallback((count = 1) => {
        setUnreadCount(prev => Math.max(0, prev - count));
    }, []);

    const resetUnreadForConversation = useCallback(async (conversationId) => {
        // Re-fetch to get accurate count after marking as read
        await refreshUnreadCount();
    }, [refreshUnreadCount]);

    return (
        <ChatContext.Provider value={{
            unreadCount,
            setUnreadCount,
            isConnected,
            refreshUnreadCount,
            decrementUnread,
            resetUnreadForConversation,
            registerMessageCallback,
            unregisterMessageCallback,
            registerReadCallback,
            unregisterReadCallback,
            setActiveConversation,
        }}>
            {children}
        </ChatContext.Provider>
    );
}

export function useChatContext() {
    const ctx = useContext(ChatContext);
    if (!ctx) {
        return {
            unreadCount: 0,
            setUnreadCount: () => { },
            isConnected: false,
            refreshUnreadCount: () => { },
            decrementUnread: () => { },
            resetUnreadForConversation: () => { },
            registerMessageCallback: () => { },
            unregisterMessageCallback: () => { },
            registerReadCallback: () => { },
            unregisterReadCallback: () => { },
            setActiveConversation: () => { },
        };
    }
    return ctx;
}

export default ChatContext;
