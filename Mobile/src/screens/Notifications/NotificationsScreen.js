import React, { useEffect, useCallback } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, RefreshControl, SafeAreaView } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { colors, typography } from '../../styles/globalStyles';
import { 
    getNotificationsThunk, 
    markAsReadThunk, 
    markAllAsReadThunk, 
    deleteNotificationThunk 
} from '../../store/slices/notificationSlice';

const NotificationsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { items, loading, unreadCount } = useSelector(state => state.notification);

    useEffect(() => {
        dispatch(getNotificationsThunk());
    }, [dispatch]);

    const handleRefresh = useCallback(() => {
        dispatch(getNotificationsThunk());
    }, [dispatch]);

    const handleMarkAllRead = () => {
        if (unreadCount > 0) {
            dispatch(markAllAsReadThunk());
        }
    };

    const handleNotificationPress = (notification) => {
        if (!notification.isRead) {
            dispatch(markAsReadThunk(notification.id));
        }
        
        // Handle routing based on notification type here
        // For example:
        // if (notification.type === 'BOOKING') {
        //     navigation.navigate('BookingDetail', { bookingId: notification.referenceId });
        // }
    };

    const handleDelete = (id) => {
        dispatch(deleteNotificationThunk(id));
    };

    const renderItem = ({ item }) => {
        return (
            <TouchableOpacity 
                style={[styles.notificationCard, !item.isRead && styles.unreadCard]}
                onPress={() => handleNotificationPress(item)}
            >
                <View style={styles.cardHeader}>
                    <View style={styles.titleContainer}>
                        {!item.isRead && <View style={styles.unreadDot} />}
                        <Text style={[styles.title, !item.isRead && styles.unreadText]}>
                            {item.title}
                        </Text>
                    </View>
                    <TouchableOpacity onPress={() => handleDelete(item.id)} style={styles.deleteButton}>
                        <Ionicons name="close" size={20} color={colors.textSecondary} />
                    </TouchableOpacity>
                </View>
                <Text style={styles.message} numberOfLines={3}>
                    {item.message}
                </Text>
                <Text style={styles.timestamp}>
                    {new Date(item.createdAt).toLocaleString()}
                </Text>
            </TouchableOpacity>
        );
    };

    return (
        <SafeAreaView style={styles.container}>
            <View style={styles.header}>
                <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
                    <Ionicons name="arrow-back" size={24} color={colors.text} />
                </TouchableOpacity>
                <Text style={styles.headerTitle}>Notifications</Text>
                {items.length > 0 && unreadCount > 0 ? (
                    <TouchableOpacity onPress={handleMarkAllRead} style={styles.headerRight}>
                        <Text style={styles.markReadText}>Mark all read</Text>
                    </TouchableOpacity>
                ) : (
                    <View style={styles.headerRight} />
                )}
            </View>

            <FlatList
                data={items}
                keyExtractor={(item) => item.id.toString()}
                renderItem={renderItem}
                refreshControl={
                    <RefreshControl 
                        refreshing={loading} 
                        onRefresh={handleRefresh} 
                        tintColor={colors.primary}
                    />
                }
                contentContainerStyle={styles.listContainer}
                ListEmptyComponent={
                    <View style={styles.emptyContainer}>
                        <Ionicons name="notifications-off-outline" size={64} color={colors.borderLight} />
                        <Text style={styles.emptyText}>You have no notifications</Text>
                    </View>
                }
            />
        </SafeAreaView>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.background,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingHorizontal: 20,
        paddingVertical: 15,
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderLight,
    },
    backButton: {
        padding: 5,
    },
    headerTitle: {
        ...typography.h3,
        color: colors.text,
    },
    headerRight: {
        minWidth: 50,
        alignItems: 'flex-end',
    },
    markReadText: {
        ...typography.body3,
        color: colors.primary,
        fontWeight: '600',
    },
    listContainer: {
        padding: 15,
        flexGrow: 1,
    },
    notificationCard: {
        backgroundColor: colors.surface,
        borderRadius: 12,
        padding: 15,
        marginBottom: 10,
        borderWidth: 1,
        borderColor: colors.borderLight,
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.05,
        shadowRadius: 4,
        elevation: 2,
    },
    unreadCard: {
        borderColor: colors.primary + '40',
        backgroundColor: colors.primary + '05',
    },
    cardHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: 8,
    },
    titleContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        flex: 1,
        paddingRight: 10,
    },
    unreadDot: {
        width: 8,
        height: 8,
        borderRadius: 4,
        backgroundColor: colors.primary,
        marginRight: 8,
    },
    title: {
        ...typography.subtitle1,
        color: colors.text,
    },
    unreadText: {
        fontWeight: '700',
        color: colors.text,
    },
    deleteButton: {
        padding: 2,
    },
    message: {
        ...typography.body2,
        color: colors.textSecondary,
        lineHeight: 20,
        marginBottom: 10,
    },
    timestamp: {
        ...typography.body3,
        color: colors.textTertiary,
        textAlign: 'right',
    },
    emptyContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        paddingVertical: 100,
    },
    emptyText: {
        ...typography.body1,
        color: colors.textTertiary,
        marginTop: 15,
    },
});

export default NotificationsScreen;
