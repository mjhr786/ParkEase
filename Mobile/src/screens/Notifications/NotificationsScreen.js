/**
 * NotificationsScreen
 * List of user notifications with mark-read and delete
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, RefreshControl, Alert } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import {
    getNotificationsThunk,
    markNotificationReadThunk,
    deleteNotificationThunk,
} from '../../store/slices/notificationSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography, shadows } from '../../styles/globalStyles';
import { formatDateTime } from '../../utils/formatters';

const NotificationItem = ({ notification, onMarkRead, onDelete }) => {
    const isUnread = !notification.isRead;

    return (
        <View style={[itemStyles.container, isUnread && itemStyles.unread]}>
            <TouchableOpacity style={itemStyles.content} onPress={onMarkRead} activeOpacity={0.7}>
                <View style={[itemStyles.dot, isUnread && itemStyles.dotActive]} />
                <View style={itemStyles.textWrap}>
                    <Text style={[itemStyles.title, isUnread && itemStyles.titleUnread]} numberOfLines={2}>
                        {notification.title || notification.message}
                    </Text>
                    {notification.body ? (
                        <Text style={itemStyles.body} numberOfLines={2}>{notification.body}</Text>
                    ) : null}
                    <Text style={itemStyles.time}>
                        {formatDateTime ? formatDateTime(notification.createdAt) : new Date(notification.createdAt).toLocaleString()}
                    </Text>
                </View>
            </TouchableOpacity>
            <TouchableOpacity onPress={onDelete} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
                <Ionicons name="close-circle-outline" size={22} color={colors.textTertiary} />
            </TouchableOpacity>
        </View>
    );
};

const itemStyles = StyleSheet.create({
    container: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: colors.surface,
        marginHorizontal: spacing.screenHorizontal,
        marginBottom: spacing.sm,
        padding: spacing.md,
        borderRadius: spacing.radius.md,
        ...shadows.sm,
    },
    unread: {
        borderLeftWidth: 3,
        borderLeftColor: colors.primary,
    },
    content: {
        flex: 1,
        flexDirection: 'row',
        alignItems: 'flex-start',
        gap: spacing.sm,
    },
    dot: {
        width: 8,
        height: 8,
        borderRadius: 4,
        backgroundColor: 'transparent',
        marginTop: 6,
    },
    dotActive: {
        backgroundColor: colors.primary,
    },
    textWrap: { flex: 1 },
    title: { ...typography.body, color: colors.textPrimary },
    titleUnread: { fontWeight: '600' },
    body: { ...typography.bodySmall, color: colors.textSecondary, marginTop: 2 },
    time: { ...typography.caption, color: colors.textTertiary, marginTop: 4 },
});

const NotificationsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { notifications, loading } = useSelector((state) => state.notification);
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getNotificationsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getNotificationsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const handleMarkRead = useCallback(
        (id) => dispatch(markNotificationReadThunk(id)),
        [dispatch]
    );

    const handleDelete = useCallback(
        (id) => {
            Alert.alert('Delete Notification', 'Remove this notification?', [
                { text: 'Cancel', style: 'cancel' },
                { text: 'Delete', style: 'destructive', onPress: () => dispatch(deleteNotificationThunk(id)) },
            ]);
        },
        [dispatch]
    );

    if (loading && !notifications.length) {
        return <LoadingScreen />;
    }

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
                    <Ionicons name="arrow-back" size={24} color={colors.textPrimary} />
                </TouchableOpacity>
                <Text style={styles.title}>Notifications</Text>
                <View style={{ width: 24 }} />
            </View>
            <FlatList
                data={notifications}
                keyExtractor={(item) => item.id?.toString()}
                renderItem={({ item }) => (
                    <NotificationItem
                        notification={item}
                        onMarkRead={() => handleMarkRead(item.id)}
                        onDelete={() => handleDelete(item.id)}
                    />
                )}
                ListEmptyComponent={
                    <EmptyState
                        icon="notifications-off-outline"
                        title="No notifications"
                        message="You're all caught up!"
                    />
                }
                refreshControl={
                    <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />
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
        paddingTop: 60,
        paddingBottom: spacing.md,
        paddingHorizontal: spacing.screenHorizontal,
    },
    title: { ...typography.h3, color: colors.textPrimary },
});

export default NotificationsScreen;
