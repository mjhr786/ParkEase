/**
 * VendorBookingsScreen
 * Incoming bookings for vendor with approve/reject actions
 */

import React, { useEffect, useCallback, useState } from 'react';
import { View, Text, FlatList, TouchableOpacity, Alert, StyleSheet, RefreshControl } from 'react-native';
import { useDispatch, useSelector } from 'react-redux';
import { Ionicons } from '@expo/vector-icons';
import { getVendorBookingsThunk, approveBookingThunk, rejectBookingThunk } from '../../store/slices/bookingSlice';
import ScreenLayout from '../../components/Layouts/ScreenLayout';
import Card from '../../components/Common/Card';
import Badge from '../../components/Common/Badge';
import Button from '../../components/Common/Button';
import EmptyState from '../../components/Common/EmptyState';
import LoadingScreen from '../../components/Common/LoadingScreen';
import { colors, spacing, typography } from '../../styles/globalStyles';
import { formatCurrency, formatDate, formatTime } from '../../utils/formatters';
import { BookingStatus } from '../../utils/constants';

const FILTERS = [
    { label: 'All', value: null },
    { label: 'Pending', value: BookingStatus.Pending },
    { label: 'Active', value: BookingStatus.Confirmed },
    { label: 'Completed', value: BookingStatus.Completed },
];

const VendorBookingsScreen = ({ navigation }) => {
    const dispatch = useDispatch();
    const { vendorBookings, vendorBookingsLoading } = useSelector((s) => s.booking);
    const [activeFilter, setActiveFilter] = useState(0);
    const [refreshing, setRefreshing] = useState(false);

    useEffect(() => {
        dispatch(getVendorBookingsThunk());
    }, [dispatch]);

    const onRefresh = useCallback(async () => {
        setRefreshing(true);
        await dispatch(getVendorBookingsThunk());
        setRefreshing(false);
    }, [dispatch]);

    const handleApprove = useCallback((id) => {
        Alert.alert('Approve Booking', 'Confirm approval?', [
            { text: 'Cancel', style: 'cancel' },
            { text: 'Approve', onPress: () => dispatch(approveBookingThunk(id)) },
        ]);
    }, [dispatch]);

    const handleReject = useCallback((id) => {
        Alert.alert('Reject Booking', 'Are you sure you want to reject this booking?', [
            { text: 'Cancel', style: 'cancel' },
            { text: 'Reject', style: 'destructive', onPress: () => dispatch(rejectBookingThunk({ id, reason: 'Rejected by vendor' })) },
        ]);
    }, [dispatch]);

    const filteredBookings = vendorBookings.filter((b) => {
        const filter = FILTERS[activeFilter].value;
        if (filter == null) return true;
        return b.status === filter;
    });

    const renderBooking = ({ item }) => (
        <Card onPress={() => navigation.navigate('BookingDetail', { bookingId: item.id })}>
            <View style={styles.cardHeader}>
                <View style={{ flex: 1 }}>
                    <Text style={styles.bookingTitle}>{item.userName}</Text>
                    <Text style={styles.parkingName}>{item.parkingSpaceTitle}</Text>
                </View>
                <Badge status={item.status} />
            </View>

            <View style={styles.detailRow}>
                <View style={styles.detailItem}>
                    <Ionicons name="calendar-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.detailText}>{formatDate(item.startDateTime)}</Text>
                </View>
                <View style={styles.detailItem}>
                    <Ionicons name="time-outline" size={14} color={colors.textTertiary} />
                    <Text style={styles.detailText}>{formatTime(item.startDateTime)} - {formatTime(item.endDateTime)}</Text>
                </View>
                <Text style={styles.amount}>{formatCurrency(item.totalAmount)}</Text>
            </View>

            {/* Actions for pending bookings */}
            {item.status === BookingStatus.Pending && (
                <View style={styles.actionRow}>
                    <Button title="Approve" onPress={() => handleApprove(item.id)} size="sm" style={{ flex: 1 }} icon={<Ionicons name="checkmark" size={18} color={colors.white} />} />
                    <Button title="Reject" onPress={() => handleReject(item.id)} size="sm" variant="danger" style={{ flex: 1 }} icon={<Ionicons name="close" size={18} color={colors.white} />} />
                </View>
            )}
        </Card>
    );

    return (
        <ScreenLayout>
            <View style={styles.header}>
                <Text style={styles.screenTitle}>Bookings</Text>
            </View>

            {/* Filters */}
            <View style={styles.filterRow}>
                {FILTERS.map((filter, idx) => (
                    <TouchableOpacity
                        key={idx}
                        onPress={() => setActiveFilter(idx)}
                        style={[styles.filterTab, activeFilter === idx && styles.filterTabActive]}
                    >
                        <Text style={[styles.filterTabText, activeFilter === idx && styles.filterTabTextActive]}>{filter.label}</Text>
                    </TouchableOpacity>
                ))}
            </View>

            {vendorBookingsLoading && !refreshing ? (
                <LoadingScreen />
            ) : (
                <FlatList
                    data={filteredBookings}
                    keyExtractor={(item) => item.id}
                    renderItem={renderBooking}
                    contentContainerStyle={styles.listContent}
                    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
                    showsVerticalScrollIndicator={false}
                    ListEmptyComponent={<EmptyState icon="calendar-outline" title="No bookings" message="Bookings for your parking spaces will show here" />}
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
    cardHeader: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: spacing.sm },
    bookingTitle: { ...typography.label, color: colors.textPrimary },
    parkingName: { ...typography.caption, color: colors.textTertiary, marginTop: 2 },
    detailRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
    detailItem: { flexDirection: 'row', alignItems: 'center', gap: 4 },
    detailText: { ...typography.caption, color: colors.textSecondary },
    amount: { ...typography.label, color: colors.primary, marginLeft: 'auto' },
    actionRow: { flexDirection: 'row', gap: spacing.md, marginTop: spacing.md, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.borderLight },
});

export default VendorBookingsScreen;
