import { createContext, useContext, useState, useCallback } from 'react';
import useNotifications from '../hooks/useNotifications';

const NotificationContext = createContext(null);

/**
 * Notification types with their corresponding styles
 */
const NOTIFICATION_STYLES = {
    'booking.requested': { icon: '📥', bg: 'rgba(59, 130, 246, 0.95)', title: 'New Request' },
    'booking.approved': { icon: '✅', bg: 'rgba(16, 185, 129, 0.95)', title: 'Approved' },
    'booking.rejected': { icon: '❌', bg: 'rgba(239, 68, 68, 0.95)', title: 'Rejected' },
    'booking.cancelled': { icon: '🚫', bg: 'rgba(107, 114, 128, 0.95)', title: 'Cancelled' },
    'payment.completed': { icon: '💰', bg: 'rgba(16, 185, 129, 0.95)', title: 'Payment' },
    'booking.checkin': { icon: '🚗', bg: 'rgba(59, 130, 246, 0.95)', title: 'Check In' },
    'booking.checkout': { icon: '👋', bg: 'rgba(107, 114, 128, 0.95)', title: 'Check Out' },
    default: { icon: '🔔', bg: 'rgba(75, 85, 99, 0.95)', title: 'Notification' }
};

/**
 * Provider component for app-wide notifications.
 * Manages SignalR connection and toast display.
 */
export function NotificationProvider({ children }) {
    const [notifications, setNotifications] = useState([]);
    const [toasts, setToasts] = useState([]);

    // Handle incoming notification
    const handleNotification = useCallback((notification) => {
        const id = Date.now().toString();
        const newNotification = { ...notification, id, read: false };

        // Add to notifications list
        setNotifications(prev => [newNotification, ...prev].slice(0, 50)); // Keep last 50

        // Show toast
        setToasts(prev => [...prev, { ...newNotification, id }]);

        // Auto-remove toast after 5 seconds
        setTimeout(() => {
            setToasts(prev => prev.filter(t => t.id !== id));
        }, 5000);
    }, []);

    const { isConnected, connectionError } = useNotifications(handleNotification);

    // Dismiss a specific toast
    const dismissToast = useCallback((id) => {
        setToasts(prev => prev.filter(t => t.id !== id));
    }, []);

    // Mark notification as read
    const markAsRead = useCallback((id) => {
        setNotifications(prev =>
            prev.map(n => n.id === id ? { ...n, read: true } : n)
        );
    }, []);

    // Clear all notifications
    const clearAll = useCallback(() => {
        setNotifications([]);
    }, []);

    const unreadCount = notifications.filter(n => !n.read).length;

    return (
        <NotificationContext.Provider
            value={{
                notifications,
                unreadCount,
                isConnected,
                connectionError,
                markAsRead,
                clearAll
            }}
        >
            {children}

            {/* Toast Container */}
            <div style={{
                position: 'fixed',
                top: '1rem',
                right: '1rem',
                zIndex: 9999,
                display: 'flex',
                flexDirection: 'column',
                gap: '0.5rem',
                maxWidth: '380px',
                pointerEvents: 'none'
            }}>
                {toasts.map(toast => {
                    const style = NOTIFICATION_STYLES[toast.type] || NOTIFICATION_STYLES.default;
                    return (
                        <div
                            key={toast.id}
                            style={{
                                background: style.bg,
                                backdropFilter: 'blur(10px)',
                                borderRadius: '12px',
                                padding: '1rem',
                                boxShadow: '0 10px 40px rgba(0,0,0,0.3)',
                                animation: 'slideIn 0.3s ease-out',
                                pointerEvents: 'auto',
                                cursor: 'pointer'
                            }}
                            onClick={() => dismissToast(toast.id)}
                        >
                            <div style={{ display: 'flex', alignItems: 'flex-start', gap: '0.75rem' }}>
                                <span style={{ fontSize: '1.5rem' }}>{style.icon}</span>
                                <div style={{ flex: 1 }}>
                                    <div style={{
                                        fontWeight: '600',
                                        marginBottom: '0.25rem',
                                        color: 'white'
                                    }}>
                                        {toast.title}
                                    </div>
                                    <div style={{
                                        fontSize: '0.875rem',
                                        opacity: 0.9,
                                        color: 'white'
                                    }}>
                                        {toast.message}
                                    </div>
                                </div>
                                <button
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        dismissToast(toast.id);
                                    }}
                                    style={{
                                        background: 'transparent',
                                        border: 'none',
                                        color: 'white',
                                        opacity: 0.7,
                                        cursor: 'pointer',
                                        fontSize: '1.25rem',
                                        padding: 0,
                                        lineHeight: 1
                                    }}
                                >
                                    ×
                                </button>
                            </div>
                        </div>
                    );
                })}
            </div>

            {/* Animation styles */}
            <style>{`
                @keyframes slideIn {
                    from {
                        transform: translateX(100%);
                        opacity: 0;
                    }
                    to {
                        transform: translateX(0);
                        opacity: 1;
                    }
                }
            `}</style>
        </NotificationContext.Provider>
    );
}

/**
 * Hook to access notification context
 */
export function useNotificationContext() {
    const context = useContext(NotificationContext);
    if (!context) {
        throw new Error('useNotificationContext must be used within NotificationProvider');
    }
    return context;
}

export default NotificationContext;
