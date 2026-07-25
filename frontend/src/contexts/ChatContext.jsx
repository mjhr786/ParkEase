import { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from '../config';
import { useAuth } from './AuthContext';
import toast from 'react-hot-toast';
import api from '../services/api';

const ChatContext = createContext(null);

function sameUserId(a, b) {
    if (a == null || b == null) return false;
    return String(a).toLowerCase() === String(b).toLowerCase();
}

function toUnreadNumber(value) {
    if (typeof value === 'number' && Number.isFinite(value)) return Math.max(0, value);
    const n = Number(value);
    return Number.isFinite(n) ? Math.max(0, n) : 0;
}

export function ChatProvider({ children }) {
    const { isAuthenticated, user } = useAuth();
    const [unreadCount, setUnreadCount] = useState(0);
    const [isConnected, setIsConnected] = useState(false);
    const connectionRef = useRef(null);
    const onMessageCallbackRef = useRef(null);
    const onReadCallbackRef = useRef(null);
    const activeConversationRef = useRef(null);
    const joinedConversationRef = useRef(null);
    const userIdRef = useRef(user?.id);
    /** Debounce mark-as-read while user is viewing a live thread (avoid N POSTs per inbound burst). */
    const markAsReadTimerRef = useRef(null);
    const pendingMarkReadConvRef = useRef(null);

    useEffect(() => {
        userIdRef.current = user?.id;
    }, [user?.id]);

    const scheduleMarkAsRead = useCallback((conversationId) => {
        if (!conversationId) return;
        pendingMarkReadConvRef.current = conversationId;
        if (markAsReadTimerRef.current) clearTimeout(markAsReadTimerRef.current);
        markAsReadTimerRef.current = setTimeout(() => {
            const id = pendingMarkReadConvRef.current;
            pendingMarkReadConvRef.current = null;
            markAsReadTimerRef.current = null;
            if (id) api.markAsRead(id).catch(() => { });
        }, 400);
    }, []);

    const invokeSafe = useCallback(async (method, ...args) => {
        const conn = connectionRef.current;
        if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;
        try {
            await conn.invoke(method, ...args);
        } catch (err) {
            console.warn(`Chat hub ${method} failed:`, err?.message || err);
        }
    }, []);

    const joinConversation = useCallback(async (conversationId) => {
        if (!conversationId) return;
        const id = String(conversationId);
        if (joinedConversationRef.current && joinedConversationRef.current !== id) {
            await invokeSafe('LeaveConversation', joinedConversationRef.current);
        }
        await invokeSafe('JoinConversation', id);
        joinedConversationRef.current = id;
    }, [invokeSafe]);

    const leaveConversation = useCallback(async (conversationId) => {
        const id = conversationId != null ? String(conversationId) : joinedConversationRef.current;
        if (!id) return;
        await invokeSafe('LeaveConversation', id);
        if (joinedConversationRef.current === id) {
            joinedConversationRef.current = null;
        }
    }, [invokeSafe]);

    // Provide a way for Chat page to set the active conversation (+ hub group)
    const setActiveConversation = useCallback((id) => {
        const prev = activeConversationRef.current;
        activeConversationRef.current = id ?? null;
        if (id) {
            joinConversation(id);
        } else if (prev) {
            leaveConversation(prev);
        }
    }, [joinConversation, leaveConversation]);

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

    // Fetch initial unread count (fast endpoint: single COUNT, cached server-side)
    const refreshUnreadCount = useCallback(async () => {
        if (!isAuthenticated) return;
        try {
            const result = await api.getUnreadCount();
            if (result?.success) {
                setUnreadCount(toUnreadNumber(result.data));
            }
        } catch {
            // Silently fail — badge will re-sync on next visibility / chat load
        }
    }, [isAuthenticated]);

    // Let Chat page push an authoritative total from the conversation list
    const syncUnreadFromConversations = useCallback((conversations) => {
        if (!Array.isArray(conversations)) return;
        const total = conversations.reduce((sum, c) => sum + toUnreadNumber(c?.unreadCount), 0);
        setUnreadCount(total);
    }, []);

    // SignalR connection
    useEffect(() => {
        if (!isAuthenticated) {
            // Disconnect if not authenticated
            if (connectionRef.current) {
                connectionRef.current.stop().catch(() => { });
                connectionRef.current = null;
                setIsConnected(false);
            }
            joinedConversationRef.current = null;
            setUnreadCount(0);
            return;
        }

        // Fetch initial unread count
        refreshUnreadCount();

        const token = localStorage.getItem('accessToken');
        if (!token) return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_BASE_URL}/hubs/chat`, {
                accessTokenFactory: () => localStorage.getItem('accessToken') || '',
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets,
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Must be > server KeepAlive (default 30s). Default client timeout is 30s and races the ping.
        connection.serverTimeoutInMilliseconds = 90000;
        connection.keepAliveIntervalInMilliseconds = 15000;

        connection.on('ReceiveMessage', (message) => {
            // Forward to Chat page callback if registered
            if (onMessageCallbackRef.current) {
                onMessageCallbackRef.current(message);
            }

            const isOwn = sameUserId(message.senderId, userIdRef.current);
            const isActive =
                message.conversationId != null &&
                String(message.conversationId) === String(activeConversationRef.current);

            // Show toast & increment unread if message is from someone else AND not in the currently active conversation
            if (!isOwn && !isActive) {
                setUnreadCount((prev) => prev + 1);

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
            } else if (isActive && !isOwn) {
                // Coalesce mark-as-read: one request per active conversation burst, not per message
                scheduleMarkAsRead(message.conversationId);
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
            joinedConversationRef.current = null;
            setIsConnected(false);
        });
        connection.onreconnected(async () => {
            setIsConnected(true);
            // Re-sync badge after reconnect (messages may have arrived while offline)
            refreshUnreadCount();
            // Re-join active conversation group after reconnect
            const active = activeConversationRef.current;
            if (active) {
                joinedConversationRef.current = null;
                try {
                    await connection.invoke('JoinConversation', String(active));
                    joinedConversationRef.current = String(active);
                } catch {
                    // non-fatal
                }
            }
        });
        connection.onreconnecting(() => setIsConnected(false));

        const timer = setTimeout(async () => {
            try {
                await connection.start();
                connectionRef.current = connection;
                setIsConnected(true);
                // Join active conversation if user already navigated to a thread
                const active = activeConversationRef.current;
                if (active) {
                    try {
                        await connection.invoke('JoinConversation', String(active));
                        joinedConversationRef.current = String(active);
                    } catch {
                        // non-fatal
                    }
                }
            } catch (err) {
                console.error('Chat SignalR connection error:', err);
            }
        }, 500);

        return () => {
            clearTimeout(timer);
            if (markAsReadTimerRef.current) {
                clearTimeout(markAsReadTimerRef.current);
                markAsReadTimerRef.current = null;
            }
            if (connectionRef.current) {
                connectionRef.current.stop().catch(() => { });
                connectionRef.current = null;
                setIsConnected(false);
            }
            joinedConversationRef.current = null;
        };
        // refreshUnreadCount is stable enough via isAuthenticated; avoid restarting hub on every re-create
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isAuthenticated, user?.id]);

    // Re-sync badge when tab becomes visible (covers missed SignalR / slow first load)
    useEffect(() => {
        if (!isAuthenticated) return;
        const onVisibility = () => {
            if (document.visibilityState === 'visible') {
                refreshUnreadCount();
            }
        };
        document.addEventListener('visibilitychange', onVisibility);
        return () => document.removeEventListener('visibilitychange', onVisibility);
    }, [isAuthenticated, refreshUnreadCount]);

    const decrementUnread = useCallback((count = 1) => {
        setUnreadCount((prev) => Math.max(0, prev - toUnreadNumber(count)));
    }, []);

    const resetUnreadForConversation = useCallback(async () => {
        // Prefer local list sync; only re-fetch if caller wants authority from server
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
            syncUnreadFromConversations,
            registerMessageCallback,
            unregisterMessageCallback,
            registerReadCallback,
            unregisterReadCallback,
            setActiveConversation,
            joinConversation,
            leaveConversation,
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
            syncUnreadFromConversations: () => { },
            registerMessageCallback: () => { },
            unregisterMessageCallback: () => { },
            registerReadCallback: () => { },
            unregisterReadCallback: () => { },
            setActiveConversation: () => { },
            joinConversation: () => { },
            leaveConversation: () => { },
        };
    }
    return ctx;
}

export default ChatContext;
