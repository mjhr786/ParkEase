import { useState, useEffect, useRef, useCallback } from 'react';
import { useNotificationContext } from '../context/NotificationContext';
import api from '../services/api';

const TYPE_ICONS = {
    BookingRequest: '📥',
    BookingConfirmed: '✅',
    BookingRejected: '❌',
    PaymentReceived: '💰',
    NewMessage: '💬',
    SystemAlert: '🔔',
    default: '🔔',
};

const TYPE_COLORS = {
    BookingRequest: '#3b82f6',
    BookingConfirmed: '#10b981',
    BookingRejected: '#ef4444',
    PaymentReceived: '#10b981',
    NewMessage: '#8b5cf6',
    SystemAlert: '#f59e0b',
    default: '#6b7280',
};

function timeAgo(dateStr) {
    const diff = (Date.now() - new Date(dateStr).getTime()) / 1000;
    if (diff < 60) return 'just now';
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
}

export default function NotificationDropdown() {
    const [isOpen, setIsOpen] = useState(false);
    const [dbNotifications, setDbNotifications] = useState([]);
    // Single source of truth for badge: always the DB unread count
    const [unreadCount, setUnreadCount] = useState(0);
    const [loading, setLoading] = useState(false);
    const [listLoaded, setListLoaded] = useState(false);
    const [page, setPage] = useState(1);
    const [hasMore, setHasMore] = useState(true);
    const dropdownRef = useRef(null);
    const loadingRef = useRef(false);

    // Listen for real-time notifications — use them only to refresh the badge count
    const { notifications: realtimeNotifs } = useNotificationContext();
    const prevRealtimeLen = useRef(0);

    // ── Fetch only the badge count (lightweight, no list) ──────────────────
    const fetchUnreadCount = useCallback(async () => {
        try {
            const response = await api.getNotifications(1, 1); // just need unreadCount
            if (response?.success && response?.data != null) {
                setUnreadCount(response.data.unreadCount ?? 0);
            }
        } catch (_) { /* silent */ }
    }, []);

    // Load badge count on mount
    useEffect(() => {
        fetchUnreadCount();
    }, []);

    // When a new real-time notification arrives:
    // - Re-fetch the count from the API (DB is already updated before SignalR fires)
    // - If dropdown is open, also refresh the list
    useEffect(() => {
        if (realtimeNotifs.length > prevRealtimeLen.current) {
            // Re-fetch count from DB — avoids race with mount fetchUnreadCount
            fetchUnreadCount();
            // Reset listLoaded so the list refreshes on next open
            setListLoaded(false);
            setDbNotifications([]);
            if (isOpen) {
                fetchList(1, false);
            }
        }
        prevRealtimeLen.current = realtimeNotifs.length;
    }, [realtimeNotifs.length]);

    // ── Fetch the full notification list ────────────────────────────────────
    const fetchList = useCallback(async (pageNum = 1, append = false) => {
        if (loadingRef.current) return;
        loadingRef.current = true;
        setLoading(true);
        try {
            const response = await api.getNotifications(pageNum, 20);
            if (response?.success && response?.data) {
                const { notifications: pagedData, unreadCount: count } = response.data;
                const items = pagedData?.items ?? [];
                setDbNotifications(prev => append ? [...prev, ...items] : items);
                setUnreadCount(count ?? 0); // keep badge in sync with DB truth
                setHasMore(pagedData?.hasNextPage ?? false);
                setListLoaded(true);
            }
        } catch (err) {
            console.error('Failed to fetch notifications:', err);
        } finally {
            setLoading(false);
            loadingRef.current = false;
        }
    }, []);

    // Load list every time the dropdown is opened (always fresh)
    useEffect(() => {
        if (isOpen) {
            fetchList(1, false);
        }
    }, [isOpen]);

    // Close on outside click
    useEffect(() => {
        const handleClickOutside = (e) => {
            if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
                setIsOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const handleOpen = () => setIsOpen(prev => !prev);

    const handleMarkRead = async (notification) => {
        if (notification.isRead) return;
        try {
            await api.markNotificationAsRead(notification.id);
            setDbNotifications(prev =>
                prev.map(n => n.id === notification.id ? { ...n, isRead: true } : n)
            );
            setUnreadCount(prev => Math.max(0, prev - 1));
        } catch (err) {
            console.error('Failed to mark as read:', err);
        }
    };

    const handleMarkAllRead = async () => {
        try {
            await api.markAllNotificationsAsRead();
            setDbNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
            setUnreadCount(0);
        } catch (err) {
            console.error('Failed to mark all as read:', err);
        }
    };

    const handleClearAll = async () => {
        if (!window.confirm('Are you sure you want to clear all notifications?')) return;
        try {
            await api.clearAllNotifications();
            setDbNotifications([]);
            setUnreadCount(0);
            setHasMore(false);
        } catch (err) {
            console.error('Failed to clear all notifications:', err);
        }
    };

    const handleDelete = async (e, notificationId) => {
        e.stopPropagation(); // Prevent triggering mark as read
        try {
            await api.deleteNotification(notificationId);
            setDbNotifications(prev => {
                const updatedList = prev.filter(n => n.id !== notificationId);
                // Also optionally decrement unread count if the deleted item was unread
                // (Though relying on the next poll/ws event is also fine).
                const wasUnread = prev.find(n => n.id === notificationId && !n.isRead);
                if (wasUnread) {
                    setUnreadCount(count => Math.max(0, count - 1));
                }
                return updatedList;
            });
        } catch (err) {
            console.error('Failed to delete notification:', err);
        }
    };

    const handleLoadMore = () => {
        const nextPage = page + 1;
        setPage(nextPage);
        fetchList(nextPage, true);
    };

    return (
        <div ref={dropdownRef} style={{ position: 'relative' }}>
            {/* Bell Button */}
            <button
                onClick={handleOpen}
                aria-label="Notifications"
                style={{
                    position: 'relative',
                    background: 'transparent',
                    border: 'none',
                    cursor: 'pointer',
                    padding: '0.4rem',
                    borderRadius: '50%',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: '1.3rem',
                    transition: 'background 0.2s',
                    color: 'inherit',
                }}
                onMouseEnter={e => e.currentTarget.style.background = 'rgba(255,255,255,0.1)'}
                onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
            >
                🔔
                {unreadCount > 0 && (
                    <span style={{
                        position: 'absolute',
                        top: '-2px',
                        right: '-2px',
                        background: '#ef4444',
                        color: 'white',
                        fontSize: '0.65rem',
                        fontWeight: '700',
                        borderRadius: '999px',
                        minWidth: '16px',
                        height: '16px',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: '0 3px',
                        lineHeight: 1,
                        boxShadow: '0 0 0 2px var(--navbar-bg, #1e293b)',
                    }}>
                        {unreadCount > 99 ? '99+' : unreadCount}
                    </span>
                )}
            </button>

            {/* Dropdown Panel */}
            {isOpen && (
                <div style={{
                    position: 'absolute',
                    top: 'calc(100% + 10px)',
                    right: 0,
                    width: '360px',
                    maxWidth: '90vw',
                    background: '#1e293b',
                    borderRadius: '16px',
                    boxShadow: '0 25px 60px rgba(0,0,0,0.5), 0 0 0 1px rgba(255,255,255,0.08)',
                    overflow: 'hidden',
                    zIndex: 9000,
                    animation: 'notifOpen 0.2s ease-out',
                }}>
                    {/* Header */}
                    <div style={{
                        padding: '1rem 1.25rem',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        borderBottom: '1px solid rgba(255,255,255,0.08)',
                    }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                            <span style={{ fontSize: '1.1rem', fontWeight: '700', color: 'white' }}>Notifications</span>
                            {unreadCount > 0 && (
                                <span style={{
                                    background: '#ef4444',
                                    color: 'white',
                                    fontSize: '0.7rem',
                                    fontWeight: '700',
                                    borderRadius: '999px',
                                    padding: '1px 7px',
                                }}>
                                    {unreadCount} unread
                                </span>
                            )}
                        </div>
                        <div style={{ display: 'flex', gap: '0.4rem' }}>
                            {unreadCount > 0 && (
                                <button
                                    onClick={handleMarkAllRead}
                                    style={{
                                        background: 'transparent',
                                        border: 'none',
                                        color: '#60a5fa',
                                        fontSize: '0.75rem',
                                        cursor: 'pointer',
                                        padding: '0.25rem 0.4rem',
                                        borderRadius: '6px',
                                        textDecoration: 'none',
                                    }}
                                    onMouseEnter={e => e.currentTarget.style.background = 'rgba(96,165,250,0.1)'}
                                    onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                                >
                                    Mark all read
                                </button>
                            )}
                            {dbNotifications.length > 0 && (
                                <button
                                    onClick={handleClearAll}
                                    style={{
                                        background: 'transparent',
                                        border: 'none',
                                        color: '#94a3b8',
                                        fontSize: '0.75rem',
                                        cursor: 'pointer',
                                        padding: '0.25rem 0.4rem',
                                        borderRadius: '6px',
                                        textDecoration: 'none',
                                    }}
                                    onMouseEnter={e => e.currentTarget.style.background = 'rgba(148,163,184,0.1)'}
                                    onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                                >
                                    Clear all
                                </button>
                            )}
                        </div>
                    </div>

                    {/* Notification List */}
                    <div style={{ maxHeight: '400px', overflowY: 'auto' }}>
                        {loading && dbNotifications.length === 0 ? (
                            <div style={{ padding: '2rem', textAlign: 'center', color: '#94a3b8' }}>
                                Loading…
                            </div>
                        ) : dbNotifications.length === 0 ? (
                            <div style={{ padding: '2.5rem', textAlign: 'center' }}>
                                <div style={{ fontSize: '2.5rem', marginBottom: '0.75rem' }}>🔕</div>
                                <div style={{ color: '#94a3b8', fontSize: '0.9rem' }}>No notifications yet</div>
                            </div>
                        ) : (
                            <>
                                {dbNotifications.map(n => {
                                    const icon = TYPE_ICONS[n.type] || TYPE_ICONS.default;
                                    const color = TYPE_COLORS[n.type] || TYPE_COLORS.default;
                                    return (
                                        <div
                                            key={n.id}
                                            onClick={() => handleMarkRead(n)}
                                            style={{
                                                padding: '0.875rem 1.25rem',
                                                display: 'flex',
                                                gap: '0.75rem',
                                                alignItems: 'flex-start',
                                                cursor: n.isRead ? 'default' : 'pointer',
                                                background: n.isRead ? 'transparent' : 'rgba(59,130,246,0.06)',
                                                borderBottom: '1px solid rgba(255,255,255,0.05)',
                                                transition: 'background 0.15s',
                                            }}
                                            onMouseEnter={e => {
                                                if (!n.isRead) e.currentTarget.style.background = 'rgba(59,130,246,0.12)';
                                            }}
                                            onMouseLeave={e => {
                                                e.currentTarget.style.background = n.isRead ? 'transparent' : 'rgba(59,130,246,0.06)';
                                            }}
                                        >
                                            <div style={{
                                                width: '36px', height: '36px', borderRadius: '50%',
                                                background: `${color}22`, display: 'flex',
                                                alignItems: 'center', justifyContent: 'center',
                                                fontSize: '1.1rem', flexShrink: 0,
                                            }}>
                                                {icon}
                                            </div>
                                            <div style={{ flex: 1, minWidth: 0 }}>
                                                <div style={{
                                                    fontWeight: n.isRead ? '500' : '600',
                                                    color: n.isRead ? '#94a3b8' : 'white',
                                                    fontSize: '0.875rem', marginBottom: '2px',
                                                    whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
                                                }}>
                                                    {n.title}
                                                </div>
                                                <div style={{
                                                    color: '#64748b', fontSize: '0.8rem', lineHeight: '1.4',
                                                    display: '-webkit-box', WebkitLineClamp: 2,
                                                    WebkitBoxOrient: 'vertical', overflow: 'hidden',
                                                }}>
                                                    {n.message}
                                                </div>
                                                <div style={{ color: '#475569', fontSize: '0.72rem', marginTop: '4px' }}>
                                                    {timeAgo(n.createdAt)}
                                                </div>
                                            </div>
                                            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '0.5rem' }}>
                                                {n.isRead && (
                                                    <button
                                                        onClick={(e) => handleDelete(e, n.id)}
                                                        title="Delete notification"
                                                        style={{
                                                            background: 'transparent', border: 'none', color: '#64748b',
                                                            cursor: 'pointer', fontSize: '1rem', padding: '0.2rem',
                                                            borderRadius: '4px', transition: 'color 0.2s',
                                                        }}
                                                        onMouseEnter={e => e.currentTarget.style.color = '#ef4444'}
                                                        onMouseLeave={e => e.currentTarget.style.color = '#64748b'}
                                                    >
                                                        🗑️
                                                    </button>
                                                )}
                                                {!n.isRead && (
                                                    <div style={{
                                                        width: '8px', height: '8px', borderRadius: '50%',
                                                        background: '#3b82f6', flexShrink: 0, marginTop: '2px',
                                                    }} />
                                                )}
                                            </div>
                                        </div>
                                    );
                                })}
                                {hasMore && (
                                    <div style={{ padding: '0.75rem', textAlign: 'center' }}>
                                        <button
                                            onClick={handleLoadMore}
                                            disabled={loading}
                                            style={{
                                                background: 'transparent',
                                                border: '1px solid rgba(255,255,255,0.1)',
                                                color: '#94a3b8', padding: '0.4rem 1rem',
                                                borderRadius: '8px', cursor: 'pointer', fontSize: '0.8rem',
                                            }}
                                        >
                                            {loading ? 'Loading…' : 'Load more'}
                                        </button>
                                    </div>
                                )}
                            </>
                        )}
                    </div>
                </div>
            )}

            <style>{`
                @keyframes notifOpen {
                    from { opacity: 0; transform: translateY(-8px) scale(0.97); }
                    to   { opacity: 1; transform: translateY(0) scale(1); }
                }
            `}</style>
        </div>
    );
}
