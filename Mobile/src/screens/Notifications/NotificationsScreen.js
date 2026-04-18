/**
 * NotificationsScreen
 * List of user notifications with mark-read and delete
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, Alert } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import {
    getNotificationsThunk,
    markNotificationReadThunk,
    deleteNotificationThunk,
    clearAllNotificationsThunk,
    markAllNotificationsAsReadThunk,
} from '../../store/slices/notificationSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { NotificationsSkeleton } from '../../components/Common/ShimmerPlaceholder';
import EnhancedRefreshControl from '../../components/Common/EnhancedRefreshControl';
import SwipeableRow from '../../components/Common/SwipeableRow';
import { EventBus } from '../../utils/EventBus';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatDateTime } from '../../utils/formatters';

const NOTIFICATION_CONFIG = {
    0: { icon: 'download-outline', color: '#3b82f6', label: 'New Request' },    // BookingRequest
    1: { icon: 'checkmark-circle-outline', color: '#10b981', label: 'Approved' }, // BookingConfirmed
    2: { icon: 'close-circle-outline', color: '#ef4444', label: 'Rejected' },    // BookingRejected
    3: { icon: 'cash-outline', color: '#10b981', label: 'Payment' },            // PaymentReceived
    4: { icon: 'chatbubble-outline', color: '#3b82f6', label: 'Message' },      // NewMessage
    5: { icon: 'alert-circle-outline', color: '#6b7280', label: 'System' },     // SystemAlert
    default: { icon: 'notifications-outline', color: colors.primary, label: 'Notification' }
};

const NotificationItem = ({ notification, onPress }) => {
    const isUnread = !notification.isRead;
    const config = NOTIFICATION_CONFIG[notification.type] || NOTIFICATION_CONFIG.default;

    return (
        <TouchableOpacity style={[itemStyles.container, isUnread && itemStyles.unread]} onPress={onPress} activeOpacity={0.8}>
            <View style={[itemStyles.iconContainer, { backgroundColor: config.color + '15' }]}>
                <Ionicons name={config.icon} size={22} color={config.color} />
            </View>
            <View style={itemStyles.textWrap}>
                <View style={itemStyles.headerRow}>
                    <Text style={itemStyles.typeLabel}>{config.label}</Text>
                    <Text style={itemStyles.time}>
                        {formatDateTime ? formatDateTime(notification.createdAt) : new Date(notification.createdAt).toLocaleString()}
                    </Text>
                </View>
                <Text style={[itemStyles.title, isUnread && itemStyles.titleUnread]} numberOfLines={1}>
                    {notification.title || notification.message}
                </Text>
                <Text style={itemStyles.body} numberOfLines={2}>
                    {notification.body || notification.message}
                </Text>
            </View>
            {isUnread && <View style={itemStyles.unreadDot} />}
        </TouchableOpacity>
    );
};

const itemStyles = StyleSheet.create({
    container: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.surface,
        padding: spacing.md,
        borderRadius: spacing.radius.lg,
        ...shadows.sm,
    },
    unread: {
        backgroundColor: colors.surfaceAlt,
    },
    iconContainer: {
        width: 44,
        height: 44,
        borderRadius: 22,
        justifyContent: 'center',
        alignItems: 'center',
        marginRight: spacing.sm,
    },
    textWrap: { flex: 1 },
    headerRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 2 },
    typeLabel: { ...typography.caption, color: colors.textTertiary, fontWeight: '600', textTransform: 'uppercase' },
    title: { ...typography.body, color: colors.textPrimary, marginBottom: 1 },
    titleUnread: { fontWeight: '700' },
    body: { ...typography.bodySmall, color: colors.textSecondary },
    time: { ...typography.caption, color: colors.textTertiary },
    unreadDot: {
        width: 8,
        height: 8,
        borderRadius: 4,
        backgroundColor: colors.primary,
        marginLeft: spacing.xs,
    },
});

const NotificationsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { notifications, loading } = useSelector((state) => state.notification);
    const [refreshing, setRefreshing] = useState(false);
    const [deletingId, setDeletingId] = useState(null);

    useEffect(() => {
        dispatch(getNotificationsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getNotificationsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const handleNotificationPress = useCallback(
        (notification) => {
            // 1. Mark as read if unread
            if (!notification.isRead) {
                dispatch(markNotificationReadThunk(notification.id));
            }

            // 2. Parse data for deep-linking
            let data = {};
            try {
                data = typeof notification.data === 'string' 
                    ? JSON.parse(notification.data) 
                    : (notification.data || {});
            } catch (e) {
                console.log('Failed to parse notification data', e);
            }

            // 3. Navigate
            const bookingId = data.BookingId || data.bookingId;
            const parkingId = data.ParkingSpaceId || data.parkingSpaceId || data.parkingId;
            const conversationId = data.ConversationId || data.conversationId;

            if (bookingId) {
                navigation.navigate('BookingDetail', { bookingId });
            } else if (parkingId) {
                navigation.navigate('ParkingDetail', { parkingId });
            } else if (conversationId) {
                navigation.navigate('ChatScreen', { conversationId });
            }
        },
        [dispatch, navigation]
    );

    const handleDelete = useCallback(
        (id) => {
            Alert.alert('Delete Notification', 'Remove this notification?', [
                { text: 'Cancel', style: 'cancel' },
                { 
                    text: 'Delete', 
                    style: 'destructive', 
                    onPress: async () => {
                        setDeletingId(id);
                        try {
                            const res = await dispatch(deleteNotificationThunk(id));
                            if (res.error) {
                                EventBus.emit('SHOW_ERROR_BANNER', { 
                                    title: 'Error', 
                                    message: res.payload || 'Failed to delete notification' 
                                });
                            } else {
                                EventBus.emit('SHOW_BANNER', {
                                    title: 'Success',
                                    message: 'Notification removed',
                                    type: 'success'
                                });
                                onRefresh();
                            }
                        } finally {
                            setDeletingId(null);
                        }
                    } 
                },
            ]);
        },
        [dispatch, onRefresh]
    );

    const handleClearAll = useCallback(() => {
        if (!notifications.length) return;
        
        Alert.alert(
            'Clear All',
            'Are you sure you want to remove all notifications?',
            [
                { text: 'Cancel', style: 'cancel' },
                { 
                    text: 'Clear All', 
                    style: 'destructive', 
                    onPress: async () => {
                        try {
                            const res = await dispatch(clearAllNotificationsThunk());
                            if (res.error) {
                                EventBus.emit('SHOW_ERROR_BANNER', { 
                                    title: 'Error', 
                                    message: res.payload || 'Failed to clear notifications' 
                                });
                            } else {
                                EventBus.emit('SHOW_BANNER', {
                                    title: 'Success',
                                    message: 'All notifications cleared',
                                    type: 'success'
                                });
                            }
                        } catch (e) {
                            console.error(e);
                        }
                    } 
                },
            ]
        );
    }, [dispatch, notifications.length]);

    if (loading && !notifications.length) {
        return (
            <ScreenLayout>
                <View style={styles.header}>
                    <TouchableOpacity onPress={() => navigation.goBack()} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
                        <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                    </TouchableOpacity>
                    <Text style={styles.title}>Notifications</Text>
                    <View style={{ width: 24 }} />
                </View>
                <NotificationsSkeleton />
            </ScreenLayout>
        );
    }

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
                    <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                </TouchableOpacity>
                <Text style={styles.title}>Notifications</Text>
                <TouchableOpacity onPress={handleClearAll} disabled={!notifications.length}>
                    <Text style={[styles.clearBtn, !notifications.length && styles.disabledBtn]}>Clear All</Text>
                </TouchableOpacity>
            </View>
            <FlatList
                data={notifications}
                keyExtractor={(item) => item.id?.toString()}
                renderItem={({ item }) => (
                    <View style={styles.rowWrap}>
                        <SwipeableRow 
                            onDelete={() => handleDelete(item.id)}
                            isDeleting={deletingId === item.id}
                            disabled={loading || !!deletingId}
                        >
                            <NotificationItem
                                notification={item}
                                onPress={() => handleNotificationPress(item)}
                            />
                        </SwipeableRow>
                    </View>
                )}
                ListEmptyComponent={
                    <EmptyState
                        icon="notifications-off-outline"
                        title="No notifications"
                        message="You're all caught up!"
                    />
                }
                refreshControl={
                    <EnhancedRefreshControl refreshing={refreshing} onRefresh={onRefresh} />
                }
                showsVerticalScrollIndicator={false}
                contentContainerStyle={{ paddingBottom: spacing['3xl'] }}
            />
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingTop: spacing.md,
        paddingBottom: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
    },
    title: { ...typography.h3, color: colors.textPrimary },
    clearBtn: { ...typography.bodySmall, color: colors.primary, fontWeight: '600' },
    disabledBtn: { color: colors.textTertiary, opacity: 0.5 },
    rowWrap: {
        marginHorizontal: spacing.screenHorizontal,
        marginBottom: spacing.sm,
    },
});

export default NotificationsScreen;
