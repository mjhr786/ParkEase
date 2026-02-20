/**
 * MyBookingsScreen
 * User's bookings list with status filter tabs
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getMyBookingsThunk } from '../../store/slices/bookingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';
import { BookingStatus } from '../../utils/constants';

const FILTERS = [
    { label: 'All', value: null },
    { label: 'Active', value: [BookingStatus.Confirmed, BookingStatus.InProgress] },
    { label: 'Completed', value: [BookingStatus.Completed] },
    { label: 'Cancelled', value: [BookingStatus.Cancelled, BookingStatus.Rejected] },
];

const MyBookingsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { myBookings, myBookingsLoading } = useSelector((s) => s.booking);
    const [activeFilter, setActiveFilter] = useState(0);
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getMyBookingsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getMyBookingsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const filteredBookings = myBookings.filter((b) => {
        const filter = FILTERS[activeFilter].value;
        if (!filter) return true;
        return filter.includes(b.status);
    });

    const renderBookingItem = ({ item }) => (
        <Card onPress={() => navigation.navigate('BookingDetail', { bookingId: item.id })} style={styles.bookingCard}>
            <View style={styles.cardHeader}>
                <Text style={styles.bookingTitle} numberOfLines={1}>{item.parkingSpaceTitle}</Text>
                <Badge status={item.status} />
            </View>
            <View style={styles.cardBody}>
                <View style={styles.infoRow}>
                    <Ionicons name="location-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.infoText} numberOfLines={1}>{item.parkingSpaceAddress || 'N/A'}</Text>
                </View>
                <View style={styles.infoRow}>
                    <Ionicons name="calendar-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.infoText}>{formatDate(item.startDateTime)}</Text>
                </View>
                <View style={styles.infoRow}>
                    <Ionicons name="time-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.infoText}>{formatTime(item.startDateTime)} - {formatTime(item.endDateTime)}</Text>
                </View>
            </View>
            <View style={styles.cardFooter}>
                <Text style={styles.refCode}>Ref: {item.bookingReference}</Text>
                <Text style={styles.amount}>{formatCurrency(item.totalAmount)}</Text>
            </View>
        </Card>
    );

    return (
        <ScreenLayout>
            {/* Header */}
            <View style={styles.header}>
                <Text style={styles.screenTitle}>My Bookings</Text>
            </View>

            {/* Filter Tabs */}
            <View style={styles.filterRow}>
                {FILTERS.map((filter, idx) => (
                    <TouchableOpacity
                        key={idx}
                        onPress={() => setActiveFilter(idx)}
                        style={[styles.filterTab, activeFilter === idx && styles.filterTabActive]}
                    >
                        <Text style={[styles.filterTabText, activeFilter === idx && styles.filterTabTextActive]}>
                            {filter.label}
                        </Text>
                    </TouchableOpacity>
                ))}
            </View>

            {/* List */}
            {myBookingsLoading && !refreshing ? (
                <LoadingScreen />
            ) : (
                <FlatList
                    data={filteredBookings}
                    keyExtractor={(item) => item.id}
                    renderItem={renderBookingItem}
                    contentContainerStyle={styles.listContent}
                    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
                    showsVerticalScrollIndicator={false}
                    ListEmptyComponent={<EmptyState icon="calendar-outline" title="No bookings" message="You don't have any bookings yet" />}
                />
            )}
        </ScreenLayout>
    );
};

const styles = StyleSheet.create({
    header: { paddingTop: 60, paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing.md },
    screenTitle: { ...typography.h2, color: colors.textPrimary },
    filterRow: { flexDirection: 'row', paddingHorizontal: spacing.screenHorizontal, gap: spacing.sm, marginBottom: spacing.md },
    filterTab: { paddingHorizontal: spacing.base, paddingVertical: spacing.sm, borderRadius: spacing.radius.full, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
    filterTabActive: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
    filterTabText: { ...typography.caption, color: colors.textSecondary, fontWeight: '500' },
    filterTabTextActive: { color: colors.primary, fontWeight: '600' },
    listContent: { paddingHorizontal: spacing.screenHorizontal, paddingBottom: spacing['2xl'] },
    bookingCard: {},
    cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.sm },
    bookingTitle: { ...typography.label, color: colors.textPrimary, flex: 1, marginRight: spacing.sm },
    cardBody: { gap: spacing.xs },
    infoRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
    infoText: { ...typography.caption, color: colors.textSecondary },
    cardFooter: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.md, paddingTop: spacing.sm, borderTopWidth: 1, borderTopColor: colors.borderLight },
    refCode: { ...typography.caption, color: colors.textTertiary },
    amount: { ...typography.h4, color: colors.primary },
});

export default MyBookingsScreen;
